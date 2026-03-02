// AmazonMqActiveMqAcceptanceTests.cs
//
// Acceptance tests for Amazon MQ (ActiveMQ) covering:
//   1. Broker provisioning via Terraform
//   2. Authentication via Vault-stored credentials
//   3. Dynamic destination (queue / topic) creation
//   4. Basic messaging capability (produce / consume)
//
// Prerequisites
//   - .NET 8 SDK
//   - Terraform ≥ 1.7 on PATH
//   - AWS credentials with AmazonMQ full-access
//   - HashiCorp Vault reachable from the test runner
//   - Test runner network can reach the broker's private IP on port 61614/61617
//
// Environment variables (minimum required):
//   VAULT_ADDR          https://vault.example.com
//   VAULT_TOKEN         s.xxxxxxxxxxxx
//   AWS_REGION          us-east-1
//   TF_VAR_vpc_id       vpc-xxxxxxxx
//   TF_VAR_subnet_ids   ["subnet-xxxxxxxx"]
//   TF_VAR_vault_address  https://vault.example.com
//
// Run:
//   dotnet test --logger "console;verbosity=detailed" -- NUnit.DefaultTimeout=3600000

using Apache.NMS;
using NUnit.Framework;

namespace AmazonMQ.AcceptanceTests;

[TestFixture]
[Category("Acceptance")]
[NonParallelizable]          // Terraform state is shared; tests must run sequentially.
public sealed class AmazonMqActiveMqAcceptanceTests
{
    // ── Shared state across tests in this fixture ─────────────────────────────
    private TestConfiguration   _config     = null!;
    private TerraformRunner     _terraform  = null!;
    private string              _endpoint   = string.Empty;
    private string              _username   = string.Empty;
    private string              _password   = string.Empty;

    // NMS objects kept alive for the messaging sub-tests
    private BrokerConnectionFactory? _factory;
    private IConnection?             _connection;
    private ISession?                _session;

    // ─────────────────────────────────────────────────────────────────────────
    // One-time setup / tear-down (maps to Go's TestAmazonMQActiveMQ)
    // ─────────────────────────────────────────────────────────────────────────

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _config   = TestConfiguration.Load();
        _terraform = new TerraformRunner(_config.TerraformDir);

        // ── 1. Provision the broker ───────────────────────────────────────────
        await ProvisionBrokerAsync();

        // ── 2. Retrieve & validate Vault credentials ──────────────────────────
        await ValidateVaultCredentialsAsync();

        // ── Open a shared NMS session used by all messaging tests ─────────────
        _factory    = new BrokerConnectionFactory(_endpoint, _username, _password);
        _connection = _factory.CreateAndStartConnection();
        _session    = _connection.CreateSession(AcknowledgementMode.ClientAcknowledge);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        // Close messaging resources
        try { _session?.Close();    } catch { /* best-effort */ }
        try { _connection?.Close(); } catch { /* best-effort */ }
        _factory?.Dispose();

        // ── Terraform destroy ─────────────────────────────────────────────────
        if (_terraform is not null)
            await _terraform.DestroyAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1 – Broker provisioning
    // ─────────────────────────────────────────────────────────────────────────

    private async Task ProvisionBrokerAsync()
    {
        TestContext.Progress.WriteLine("=== 1. Broker Provisioning (Terraform) ===");

        await _terraform.InitAsync();
        await _terraform.ApplyAsync();

        var brokerId = await _terraform.OutputAsync("broker_id");
        Assert.That(brokerId, Is.Not.Null.And.Not.Empty,
            "terraform output 'broker_id' must not be empty after apply");

        var brokerArn = await _terraform.OutputAsync("broker_arn");
        Assert.That(brokerArn, Does.StartWith("arn:aws:mq:"),
            $"'broker_arn' must be a valid Amazon MQ ARN; got: {brokerArn}");

        _endpoint = await _terraform.OutputAsync("broker_endpoint");
        Assert.That(_endpoint, Is.Not.Null.And.Not.Empty,
            "terraform output 'broker_endpoint' must not be empty");

        TestContext.Progress.WriteLine($"Broker endpoint: {_endpoint}");

        // Poll until the broker is accepting TCP connections
        await BrokerReadinessPoller.WaitForBrokerReadyAsync(
            _endpoint, _config.BrokerReadyTimeout);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2 – Vault credential retrieval
    // ─────────────────────────────────────────────────────────────────────────

    private async Task ValidateVaultCredentialsAsync()
    {
        TestContext.Progress.WriteLine("=== 2. Vault Credential Retrieval ===");

        Assert.That(_config.VaultAddress, Is.Not.Null.And.Not.Empty,
            "VAULT_ADDR environment variable must be set");
        Assert.That(_config.VaultToken, Is.Not.Null.And.Not.Empty,
            "VAULT_TOKEN environment variable must be set");

        var vault = new VaultCredentialProvider(
            _config.VaultAddress,
            _config.VaultToken,
            _config.VaultSecretPath);

        (_username, _password) = await vault.GetCredentialsAsync();

        Assert.That(_username, Is.Not.Null.And.Not.Empty,
            "Vault secret must contain a non-empty 'username' key");
        Assert.That(_password, Is.Not.Null.And.Not.Empty,
            "Vault secret must contain a non-empty 'password' key");

        TestContext.Progress.WriteLine($"Vault credentials retrieved for user: {_username}");

        // Cross-check: the username Vault returned must match what Terraform used.
        var tfUsername = await _terraform.OutputAsync("broker_username");
        Assert.That(_username, Is.EqualTo(tfUsername),
            "Vault username must match the username Terraform provisioned the broker with");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3 – Dynamic destination creation  (queue)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    [Order(1)]
    public void Queue_DynamicCreation_ShouldSucceed()
    {
        const string destination = "acceptance.test.queue.creation";

        using var producer = CreateProducer(destination, isQueue: true);
        var msg = _session!.CreateTextMessage("destination-creation-probe");
        msg.NMSDeliveryMode = MsgDeliveryMode.NonPersistent;

        Assert.DoesNotThrow(
            () => producer.Send(msg),
            $"Sending to new queue '{destination}' must succeed (triggers dynamic creation)");

        TestContext.Progress.WriteLine($"Dynamic queue created: /queue/{destination}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3 – Dynamic destination creation  (topic)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    [Order(2)]
    public void Topic_DynamicCreation_ShouldSucceed()
    {
        const string destination = "acceptance.test.topic.creation";

        using var producer = CreateProducer(destination, isQueue: false);
        var msg = _session!.CreateTextMessage("destination-creation-probe");
        msg.NMSDeliveryMode = MsgDeliveryMode.NonPersistent;

        Assert.DoesNotThrow(
            () => producer.Send(msg),
            $"Sending to new topic '{destination}' must succeed (triggers dynamic creation)");

        TestContext.Progress.WriteLine($"Dynamic topic created: /topic/{destination}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4 – Queue produce / consume  (single message)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    [Order(3)]
    public void Queue_ProduceConsume_RoundTrip_ShouldSucceed()
    {
        const string destination = "acceptance.test.queue.messaging";
        const string payload     = "Hello from acceptance test – queue!";

        using var consumer = CreateConsumer(destination, isQueue: true);
        using var producer = CreateProducer(destination, isQueue: true);

        producer.Send(_session!.CreateTextMessage(payload));

        var received = ReceiveWithTimeout(consumer, _config.MessageReceiveTimeout);

        Assert.That(received, Is.Not.Null, "A message must be received within the timeout");
        Assert.That(((ITextMessage)received!).Text, Is.EqualTo(payload),
            "Received message body must match the sent payload");

        received.Acknowledge();
        TestContext.Progress.WriteLine($"Queue round-trip OK – /queue/{destination}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4 – Topic produce / consume  (single message)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    [Order(4)]
    public void Topic_ProduceConsume_RoundTrip_ShouldSucceed()
    {
        const string destination = "acceptance.test.topic.messaging";
        const string payload     = "Hello from acceptance test – topic!";

        // Subscribe BEFORE publishing – topics only deliver to live subscribers.
        using var consumer = CreateConsumer(destination, isQueue: false);

        Thread.Sleep(500); // Let the broker register the subscription

        using var producer = CreateProducer(destination, isQueue: false);
        producer.Send(_session!.CreateTextMessage(payload));

        var received = ReceiveWithTimeout(consumer, _config.MessageReceiveTimeout);

        Assert.That(received, Is.Not.Null, "A message must be received within the timeout");
        Assert.That(((ITextMessage)received!).Text, Is.EqualTo(payload),
            "Received topic message must match the sent payload");

        TestContext.Progress.WriteLine($"Topic round-trip OK – /topic/{destination}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4 – Queue multiple messages  (ordered batch)
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    [Order(5)]
    public void Queue_MultipleMessages_ShouldBeReceivedInOrder()
    {
        const string destination = "acceptance.test.queue.multi";
        int          count       = _config.MultiMessageCount;

        using var consumer = CreateConsumer(destination, isQueue: true);
        using var producer = CreateProducer(destination, isQueue: true);

        // Produce all messages first
        for (int i = 0; i < count; i++)
            producer.Send(_session!.CreateTextMessage($"message-{i}"));

        // Consume and assert in order
        for (int i = 0; i < count; i++)
        {
            var received = ReceiveWithTimeout(consumer, _config.MessageReceiveTimeout);
            Assert.That(received, Is.Not.Null, $"Message {i} must be received within the timeout");
            Assert.That(((ITextMessage)received!).Text, Is.EqualTo($"message-{i}"),
                $"Message {i} body must match");
            received.Acknowledge();
        }

        TestContext.Progress.WriteLine(
            $"{count} messages round-tripped OK on /queue/{destination}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private IMessageProducer CreateProducer(string destinationName, bool isQueue)
    {
        var dest = isQueue
            ? (IDestination)_session!.GetQueue(destinationName)
            : _session!.GetTopic(destinationName);
        return _session!.CreateProducer(dest);
    }

    private IMessageConsumer CreateConsumer(string destinationName, bool isQueue)
    {
        var dest = isQueue
            ? (IDestination)_session!.GetQueue(destinationName)
            : _session!.GetTopic(destinationName);
        return _session!.CreateConsumer(dest);
    }

    /// <summary>
    /// Synchronously receives one message within <paramref name="timeout"/>.
    /// Returns <c>null</c> if no message arrives in time (callers assert on that).
    /// </summary>
    private static IMessage? ReceiveWithTimeout(IMessageConsumer consumer, TimeSpan timeout)
        => consumer.Receive(timeout);
}
