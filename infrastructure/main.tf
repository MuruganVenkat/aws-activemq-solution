provider "aws" {
  region = var.region
}

# 1. Security Group
resource "aws_security_group" "mq_sg" {
  name        = "${var.broker_name}-sg"
  description = "Allow inbound traffic to ActiveMQ from On-Premises"
  vpc_id      = var.vpc_id

  # OpenWire (Console/NMS)
  ingress {
    from_port   = 61616
    to_port     = 61616
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
    description = "OpenWire (SSL)"
  }
  
  # WSS (WebSockets)
  ingress {
    from_port   = 61619
    to_port     = 61619
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
    description = "WSS"
  }

  # Console / REST API
  ingress {
    from_port   = 8161
    to_port     = 8161
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
    description = "Web Console & REST API"
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# 2. Broker Execution Role (CloudWatch Logging)
resource "aws_iam_role" "mq_broker_role" {
  name = "${var.broker_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "mq.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy" "mq_logging_policy" {
  name = "${var.broker_name}-logging-policy"
  role = aws_iam_role.mq_broker_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Effect   = "Allow"
        Resource = "arn:aws:logs:*:*:*/aws/amazonmq/*"
      }
    ]
  })
}

# 3. ActiveMQ Configuration (XML)
resource "aws_mq_configuration" "mq_config" {
  description    = "Custom Configuration for Granular Access Control and DLQ"
  name           = "${var.broker_name}-config"
  engine_type    = "ActiveMQ"
  engine_version = var.broker_engine_version

  data = <<DATA
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<broker xmlns="http://activemq.apache.org/schema/core">
  <destinationPolicy>
    <policyMap>
      <policyEntries>
        <!-- Individual DLQ Strategy -->
        <policyEntry queue=">">
          <deadLetterStrategy>
            <individualDeadLetterStrategy queuePrefix="DLQ." useQueueForQueueMessages="true" />
          </deadLetterStrategy>
        </policyEntry>
        
        <!-- Individual DLQ Strategy for Topics (Durable Subs) -->
        <policyEntry topic=">">
          <deadLetterStrategy>
            <individualDeadLetterStrategy topicPrefix="DLQ." useQueueForTopicMessages="true" />
          </deadLetterStrategy>
        </policyEntry>
      </policyEntries>
    </policyMap>
  </destinationPolicy>

  <plugins>
    <!-- Authorization Plugin using Groups defined in aws_mq_user -->
    <authorizationPlugin>
      <map>
        <authorizationMap>
          <authorizationEntries>
            <authorizationEntry queue=">" read="admins" write="admins" admin="admins" />
            <authorizationEntry topic=">" read="admins" write="admins" admin="admins" />
            
            <authorizationEntry queue="finance.>" read="consumers,admins" write="producers,admins" admin="admins" />
            <authorizationEntry topic="events.>" read="consumers,admins" write="producers,admins" admin="admins" />
            
            <authorizationEntry topic="ActiveMQ.Advisory.>" read="everyone,admins,consumers,producers" write="everyone,admins,consumers,producers" admin="everyone,admins,consumers,producers" />
          </authorizationEntries>
        </authorizationMap>
      </map>
    </authorizationPlugin>
  </plugins>
</broker>
DATA
}

# 4. Amazon MQ Broker
resource "aws_mq_broker" "activemq" {
  broker_name = var.broker_name

  engine_type        = "ActiveMQ"
  engine_version     = var.broker_engine_version
  storage_type       = "ebs"
  host_instance_type = var.broker_instance_type
  
  # Deploy in Private Subnet, Multi-AZ
  deployment_mode    = "active-standby-multi-az"
  subnet_ids         = var.subnet_ids 
  security_groups    = [aws_security_group.mq_sg.id]
  publicly_accessible = false
  
  configuration {
    id       = aws_mq_configuration.mq_config.id
    revision = aws_mq_configuration.mq_config.latest_revision
  }

  user {
    username       = var.mq_admin_user
    password       = var.mq_admin_password
    console_access = true
    groups         = ["admins"]
  }

  # Additional application users must be added here or via aws_mq_user resource *associated with the broker*
  # Terraform aws_mq_broker block supports multiple 'user' blocks.
  # Using aws_mq_user resource is separate but linked.
  # For simplicity during creation, we'll add the App user here.
  
  user {
    username       = var.mq_app_user
    password       = var.mq_app_password
    console_access = false
    groups         = ["producers", "consumers", "everyone"]
  }

  logs {
    general = true
    audit   = true
  }
}

# 5. SSM Parameter for Endpoints (Updates secrets.tf idea)
resource "aws_ssm_parameter" "mq_endpoints_https" {
  name        = "/${var.broker_name}/endpoints/https"
  description = "List of HTTPS endpoints for ActiveMQ"
  type        = "StringList"
  # This extraction is simplistic; for production, improved parsing of the 'instances' list map is recommended.
  value       = join(",", [for i in aws_mq_broker.activemq.instances : i.console_url]) 
}
