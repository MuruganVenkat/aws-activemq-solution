output "broker_id" {
  value = aws_mq_broker.activemq.id
}

output "broker_arn" {
  value = aws_mq_broker.activemq.arn
}

output "broker_console_url" {
  value = aws_mq_broker.activemq.instances[0].console_url
}

output "broker_endpoints" {
  description = "List of all broker endpoints"
  value       = aws_mq_broker.activemq.instances[*].endpoints
}

output "secret_arn" {
  description = "ARN of the secret containing broker credentials"
  value       = aws_secretsmanager_secret.mq_credentials.arn
}

output "ssm_endpoint_param" {
  description = "Name of the SSM parameter storing endpoints"
  value       = aws_ssm_parameter.mq_endpoints_https.name
}
