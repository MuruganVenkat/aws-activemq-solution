using Microsoft.Extensions.Configuration;

namespace AmazonMQ.AcceptanceTests;

/// <summary>
/// Centralises all external configuration consumed by the acceptance tests.
/// Values are resolved in order: environment variables → appsettings.test.json.
/// </summary>
public sealed class TestConfiguration
{
    // ── Terraform ─────────────────────────────────────────────────────────────
    /// <summary>Path to the Terraform working directory (relative or absolute).</summary>
    public string TerraformDir { get; init; } = "../terraform";

    // ── Vault ─────────────────────────────────────────────────────────────────
    public string VaultAddress    { get; init; } = string.Empty;
    public string VaultToken      { get; init; } = string.Empty;
    public string VaultSecretPath { get; init; } = "secret/activemq/broker";

    // ── AWS ───────────────────────────────────────────────────────────────────
    public string AwsRegion { get; init; } = "us-east-1";

    // ── Messaging ─────────────────────────────────────────────────────────────
    public TimeSpan BrokerReadyTimeout  { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan MessageReceiveTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int      MultiMessageCount   { get; init; } = 5;

    // ── Factory ──────────────────────────────────────────────────────────────

    public static TestConfiguration Load()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return new TestConfiguration
        {
            TerraformDir        = config["TERRAFORM_DIR"]         ?? "../terraform",
            VaultAddress        = config["VAULT_ADDR"]            ?? config["VaultAddress"]    ?? string.Empty,
            VaultToken          = config["VAULT_TOKEN"]           ?? string.Empty,
            VaultSecretPath     = config["VAULT_SECRET_PATH"]     ?? "secret/activemq/broker",
            AwsRegion           = config["AWS_REGION"]            ?? "us-east-1",
            BrokerReadyTimeout  = TimeSpan.FromMinutes(
                                    double.TryParse(config["BROKER_READY_TIMEOUT_MINUTES"], out var m) ? m : 10),
            MessageReceiveTimeout = TimeSpan.FromSeconds(
                                    double.TryParse(config["MESSAGE_RECEIVE_TIMEOUT_SECONDS"], out var s) ? s : 30),
            MultiMessageCount   = int.TryParse(config["MULTI_MESSAGE_COUNT"], out var n) ? n : 5,
        };
    }
}
