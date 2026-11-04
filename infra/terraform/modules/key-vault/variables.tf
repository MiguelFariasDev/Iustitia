variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "tenant_id" { type = string }
variable "purge_protection_enabled" { type = bool }

variable "reader_principal_id" {
  description = "Identidade dos workloads (Key Vault Secrets User)."
  type        = string
}

variable "writer_principal_ids" {
  description = "Quem grava segredos (Key Vault Secrets Officer): quem roda o Terraform e a identidade do GitHub Actions."
  type        = list(string)
}

variable "generated_secrets" {
  description = "Segredos gerados pela própria infraestrutura (nome -> valor)."
  type        = map(string)
  sensitive   = true
}

variable "generated_secrets_version" {
  description = "Incremente para regravar os segredos gerados (value_wo não detecta mudança sozinho)."
  type        = number
  default     = 1
}

variable "external_secrets" {
  description = "Nomes dos segredos cujo valor é definido fora do Terraform."
  type        = list(string)
}

variable "private_endpoint_enabled" { type = bool }

variable "private_endpoint_subnet_id" {
  type    = string
  default = null
}

variable "private_dns_zone_id" {
  type    = string
  default = null
}

variable "allowed_ips" {
  type    = list(string)
  default = []
}

variable "tags" { type = map(string) }
