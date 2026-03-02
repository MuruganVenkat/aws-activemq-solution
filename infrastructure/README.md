# Amazon MQ (ActiveMQ) – C# Acceptance Tests

End-to-end acceptance tests written in **C# / NUnit** covering:

1. **Broker provisioning** via Terraform  
2. **Authentication** using credentials stored in HashiCorp Vault  
3. **Dynamic destination creation** (queue & topic)  
4. **Basic produce / consume messaging** over STOMP+TLS  

---

## Solution layout

```
csharp/
├── AmazonMQ.AcceptanceTests.sln
└── AmazonMQ.AcceptanceTests/
    ├── AmazonMQ.AcceptanceTests.csproj
    ├── appsettings.test.json              ← optional local config
    ├── AmazonMqActiveMqAcceptanceTests.cs ← NUnit test fixture
    ├── TestConfiguration.cs               ← env-var / JSON config loader
    └── Infrastructure/
        ├── TerraformRunner.cs             ← CLI wrapper (CliWrap)
        ├── VaultCredentialProvider.cs     ← Vault KV reader (VaultSharp)
        ├── BrokerConnectionFactory.cs     ← NMS/STOMP TLS connections
        └── BrokerReadinessPoller.cs       ← TCP readiness polling (Polly)
```

Terraform files live alongside at `../terraform/` (unchanged from the Go suite).

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | ≥ 8.0 |
| Terraform | ≥ 1.7 |
| AWS CLI | configured credentials |
| HashiCorp Vault | reachable from test runner |

---

## Key NuGet packages

| Package | Purpose |
|---------|---------|
| `NUnit` + `NUnit3TestAdapter` | Test framework & runner adapter |
| `Apache.NMS` + `Apache.NMS.STOMP` | ActiveMQ client over STOMP+TLS |
| `VaultSharp` | HashiCorp Vault KV v1/v2 reader |
| `AWSSDK.MQ` | (optional) AWS API assertions |
| `CliWrap` | Terraform CLI subprocess wrapper |
| `Polly` | Resilient TCP polling loop |

---

## Configuration

### Environment variables (preferred for CI)

```sh
export VAULT_ADDR="https://vault.example.com"
export VAULT_TOKEN="s.xxxxxxxxxxxx"
export VAULT_SECRET_PATH="secret/activemq/broker"  # optional, this is the default

export AWS_REGION="us-east-1"

# Terraform input variables
export TF_VAR_vpc_id="vpc-xxxxxxxx"
export TF_VAR_subnet_ids='["subnet-xxxxxxxx"]'
export TF_VAR_vault_address="https://vault.example.com"
```

### Local development (`appsettings.test.json`)

Copy `appsettings.test.json` and fill in the values. Environment variables
take precedence over the JSON file.

### Vault secret format

```sh
vault kv put secret/activemq/broker \
  username="activemq-admin" \
  password="$(openssl rand -base64 32)"
```

---

## Running the tests

```sh
cd csharp

# Restore packages
dotnet restore

# Run the full acceptance suite (broker provisioning takes ~10 min)
dotnet test \
  --logger "console;verbosity=detailed" \
  -- NUnit.DefaultTimeout=3600000
```

Run a single test by name:

```sh
dotnet test --filter "FullyQualifiedName~Queue_ProduceConsume"
```

---

## Test breakdown

### `OneTimeSetUp` → `ProvisionBrokerAsync`
- Runs `terraform init && apply` via `TerraformRunner`
- Asserts `broker_id` (non-empty) and `broker_arn` (starts with `arn:aws:mq:`)
- Polls port 61617 with Polly until the broker accepts TCP connections

### `OneTimeSetUp` → `ValidateVaultCredentialsAsync`
- Reads `username` / `password` from Vault via VaultSharp (KV v1 & v2)
- Asserts both keys are non-empty
- Cross-checks Vault username against the `broker_username` Terraform output

### `Queue_DynamicCreation_ShouldSucceed` (Order 1)
- Sends a non-persistent probe to a brand-new queue name
- Asserts no exception (ActiveMQ creates the queue on first publish)

### `Topic_DynamicCreation_ShouldSucceed` (Order 2)
- Same as above for a topic destination

### `Queue_ProduceConsume_RoundTrip_ShouldSucceed` (Order 3)
- Subscribes then publishes one message to a queue
- Asserts exact payload is received within 30 s and then ACKs it

### `Topic_ProduceConsume_RoundTrip_ShouldSucceed` (Order 4)
- Subscribes first (topics require a live subscriber), waits 500 ms
- Publishes one message and asserts receipt

### `Queue_MultipleMessages_ShouldBeReceivedInOrder` (Order 5)
- Publishes `MULTI_MESSAGE_COUNT` (default: 5) messages
- Receives and ACKs all, asserting sequential `message-0` … `message-N` order

---

## Cleanup

`OneTimeTearDown` calls `terraform destroy` automatically. If the run is
interrupted, destroy manually:

```sh
cd ../terraform && terraform destroy -auto-approve
```

---

## Network requirements

The test runner must reach the broker's private IP:

| Port  | Purpose |
|-------|---------|
| 61617 | OpenWire+TLS (readiness poll) |
| 61614 | STOMP+TLS (NMS messaging) |

Use a VPN, AWS SSM tunnel, or run from inside the same VPC.
