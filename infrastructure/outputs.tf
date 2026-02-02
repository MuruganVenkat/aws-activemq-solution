# Broker Identification
output "broker_id" {
  description = "The unique ID of the Amazon MQ broker"
  value       = aws_mq_broker.activemq.id
}

output "broker_arn" {
  description = "The ARN of the Amazon MQ broker"
  value       = aws_mq_broker.activemq.arn
}

# Web Console URL
output "web_console_url" {
  description = "The URL of the ActiveMQ web console (from first instance)"
  value       = aws_mq_broker.activemq.instances[0].console_url
}

# OpenWire SSL Endpoints
locals {
  # Extract OpenWire SSL URLs from all instances
  # Each instance has multiple endpoints, we filter for the ssl:// protocol
  openwire_ssl_urls = flatten([
    for instance in aws_mq_broker.activemq.instances : [
      for endpoint in instance.endpoints : endpoint
      if can(regex("^ssl://", endpoint))
    ]
  ])
}

output "openwire_ssl_active_url" {
  description = "OpenWire SSL endpoint for the active broker instance"
  value       = length(local.openwire_ssl_urls) > 0 ? local.openwire_ssl_urls[0] : null
}

output "openwire_ssl_standby_url" {
  description = "OpenWire SSL endpoint for the standby broker instance"
  value       = length(local.openwire_ssl_urls) > 1 ? local.openwire_ssl_urls[1] : null
}

output "openwire_ssl_urls" {
  description = "Collection of all OpenWire SSL endpoints (active and standby)"
  value       = local.openwire_ssl_urls
}

# All Endpoints (for reference)
output "broker_endpoints" {
  description = "List of all broker endpoints for all instances"
  value       = aws_mq_broker.activemq.instances[*].endpoints
}

# Secrets Manager
output "credentials_secret_arn" {
  description = "ARN of the secret containing broker credentials"
  value       = aws_secretsmanager_secret.mq_credentials.arn
}

output "openwire_endpoints_secret_arn" {
  description = "ARN of the secret containing OpenWire SSL endpoints"
  value       = aws_secretsmanager_secret.mq_openwire_endpoints.arn
}

# SSM Parameters
output "ssm_endpoint_param" {
  description = "Name of the SSM parameter storing HTTPS endpoints"
  value       = aws_ssm_parameter.mq_endpoints_https.name
}
