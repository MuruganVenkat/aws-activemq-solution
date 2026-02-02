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

# 3. AWS Secrets Manager: Store OpenWire SSL Endpoints
# This allows applications to discover OpenWire SSL endpoints for both active and standby brokers.

resource "aws_secretsmanager_secret" "mq_openwire_endpoints" {
  name        = "/${var.broker_name}/endpoints/openwire-ssl"
  description = "OpenWire SSL endpoints for ActiveMQ (active and standby)"
}

resource "aws_secretsmanager_secret_version" "mq_openwire_endpoints_val" {
  secret_id = aws_secretsmanager_secret.mq_openwire_endpoints.id
  
  # Use locals from outputs.tf to get filtered OpenWire SSL URLs
  secret_string = jsonencode({
    active_url  = length(local.openwire_ssl_urls) > 0 ? local.openwire_ssl_urls[0] : ""
    standby_url = length(local.openwire_ssl_urls) > 1 ? local.openwire_ssl_urls[1] : ""
    all_urls    = local.openwire_ssl_urls
  })
}
