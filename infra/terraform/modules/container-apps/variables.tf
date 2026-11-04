variable "project" { type = string }
variable "environment" { type = string }
variable "aspnetcore_environment" { type = string }
variable "app_name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "log_analytics_workspace_id" { type = string }

variable "infrastructure_subnet_id" {
  description = "Subnet delegada ao environment (perfil com rede privada). null = rede gerenciada pelo Azure."
  type        = string
  default     = null
}

variable "identity_id" { type = string }
variable "identity_client_id" { type = string }
variable "registry_login_server" { type = string }
variable "image_tag" { type = string }
variable "key_vault_uri" { type = string }
variable "migrations_secret_id" { type = string }
variable "cors_allowed_origin" { type = string }

variable "api" {
  type = object({
    cpu                        = number
    memory                     = string
    min_replicas               = number
    max_replicas               = number
    http_concurrent_requests   = number
    cpu_utilization_percent    = number
    memory_utilization_percent = number
  })
}

variable "worker" {
  type = object({
    cpu                        = number
    memory                     = string
    min_replicas               = number
    max_replicas               = number
    cpu_utilization_percent    = number
    memory_utilization_percent = number
  })
}

variable "tags" { type = map(string) }
