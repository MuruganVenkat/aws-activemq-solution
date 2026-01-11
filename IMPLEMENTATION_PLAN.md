# AWS ActiveMQ Implementation Plan

# Goal Description
Provision a secure, Private AWS ActiveMQ (Active/Standby) cluster using Terraform and develop C# .NET applications for Producing, Consuming, and **Automating Infrastructure Deployment**.

## User Review Required
> [!IMPORTANT]
> **Prerequisites**: This plan assumes you have an existing VPC and Private Subnets reachable via VPN/Direct Connect. I will use placeholder IDs in `terraform.tfvars` that you must update.

## Proposed Changes

### Infrastructure (Terraform)
Directory: `infrastructure/`

#### [NEW] [main.tf](https://github.com/muruganvenkat/aws-activemq-solution/blob/main/infrastructure/main.tf)
*   **Resources**: Broker, Configuration, Users, Security Groups, IAM Roles.

#### [NEW] [secrets.tf](https://github.com/muruganvenkat/aws-activemq-solution/blob/main/infrastructure/secrets.tf)
*   **Resources**: Secrets Manager (Credentials) and SSM (Endpoints).

### Application (C# .NET)
Directory: `src/`

#### [NEW] [ActiveMQProducer](https://github.com/muruganvenkat/aws-activemq-solution/blob/main/src/ActiveMQProducer/Program.cs)
*   **Type**: Console App.
*   **Logic**: REST API Producer.

#### [NEW] [ActiveMQClient.csproj](https://github.com/muruganvenkat/aws-activemq-solution/blob/main/src/ActiveMQClient/ActiveMQClient.csproj)
*   .NET 9.0 Console Application.
*   **Logic**: OpenWire Consumer.

#### [NEW] [TerraformApi](https://github.com/muruganvenkat/aws-activemq-solution/blob/main/src/TerraformApi/Program.cs)
*   **Type**: ASP.NET Core Web API.
*   **Endpoints**:
    *   `POST /api/terraform/init`: Runs `terraform init`.
    *   `POST /api/terraform/plan`: Runs `terraform plan -var-file=...` based on Environment.
    *   `POST /api/terraform/apply`: Runs `terraform apply -auto-approve ...`.
*   **Configuration**:
    *   `environments.json`: Defines paths and `.tfvars` for environments (Dev, Prod).
*   **Service**: `TerraformRunnerService` executes shell commands.

## Verification Plan

### Automated Tests
*   `terraform validate`
*   `dotnet build`

### Manual Verification
1.  **Deploy**: Run `terraform apply`.
2.  **Verify**: Check AWS Console.
3.  **Run API**: Start `TerraformApi` and trigger `Init` -> `Plan` -> `Apply` via Swagger UI.
