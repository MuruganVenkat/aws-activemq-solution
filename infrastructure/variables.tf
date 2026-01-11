variable "region" {
  description = "AWS Region"
  type        = string
  default     = "us-east-1"
}

variable "vpc_id" {
  description = "The ID of the VPC where the broker will be deployed"
  type        = string
}

variable "subnet_ids" {
  description = "List of Private Subnet IDs for Multi-AZ deployment (must be in different AZs)"
  type        = list(string)
}

variable "allowed_cidr_blocks" {
  description = "List of CIDR blocks allowed to access the broker (e.g., On-Premises VPN CIDR)"
  type        = list(string)
  default     = ["10.0.0.0/8"] # Example On-Prem CIDR
}

variable "broker_name" {
  description = "Name of the Amazon MQ Broker"
  type        = string
  default     = "private-activemq-cluster"
}

variable "broker_engine_version" {
  description = "Version of ActiveMQ"
  type        = string
  default     = "5.17.6"
}

variable "broker_instance_type" {
  description = "Instance type for the broker"
  type        = string
  default     = "mq.m5.large"
}

variable "mq_admin_user" {
  description = "Username for the initial ActiveMQ user"
  type        = string
  default     = "admin"
}

variable "mq_admin_password" {
  description = "Password for the initial ActiveMQ user"
  type        = string
  sensitive   = true
}

variable "mq_app_user" {
  description = "Username for the Application user (Producer/Consumer)"
  type        = string
  default     = "app_user"
}

variable "mq_app_password" {
  description = "Password for the Application user"
  type        = string
  sensitive   = true
}
