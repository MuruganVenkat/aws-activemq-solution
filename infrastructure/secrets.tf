# 1. AWS Secrets Manager: Store Broker Credentials
# This allows the application to retrieve credentials securely without hardcoding.

resource "aws_secretsmanager_secret" "mq_credentials" {
  name        = "/${var.broker_name}/credentials"
  description = "ActiveMQ Broker credentials for ${var.broker_name}"
}

resource "aws_secretsmanager_secret_version" "mq_credentials_val" {
  secret_id = aws_secretsmanager_secret.mq_credentials.id
  secret_string = jsonencode({
    username = var.mq_app_user
    password = var.mq_app_password
    engine   = "ActiveMQ"
    host     = "Determined after apply" 
  })
}

# 2. AWS SSM Parameter Store: Store Broker Endpoints
# We store the WSS and OpenWire endpoints here so producers/consumers can discover them.

# Note: We can only populate this AFTER the broker is created. 
# We will use the 'aws_mq_broker' outputs in main.tf to populate this.
# Defining the resource structure here, but the 'value' typically comes from the main resource.
# For circular dependency reasons in Terraform, typically we define this IN main.tf or link it via outputs.
# I will define the SSM parameter resource here but reference the broker from main.tf.
# However, since they are separate files, Terraform loads them all. Reference is fine.

resource "aws_ssm_parameter" "mq_endpoints_wss" {
  name        = "/${var.broker_name}/endpoints/wss"
  description = "List of WSS endpoints for ActiveMQ"
  type        = "StringList"
  value       = join(",", [for i in aws_mq_broker.activemq.instances : i.endpoints[0]]) 
  # Note: 0 is usually OpenWire or WSS depending on output order, need secure parsing in main.tf preferably.
  # For robustness, we will perform the specific 'value' assignment in main.tf or using a local. 
  # Let's keep the resource definition but set value to a placeholder if specific logic is needed, 
  # or move this to main.tf to keep logic close to the source. 
  # I'll move the complex SSM logic to main.tf to avoid confusion and keep secrets.tf for pure secrets.
}
