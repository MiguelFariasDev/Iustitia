variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "replication_type" { type = string }

variable "retention_days" {
  type = object({
    daily   = number
    weekly  = number
    monthly = number
    storage = number
  })
}

variable "writer_principal_id" {
  description = "Identidade do GitHub Actions (workflows de backup)."
  type        = string
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
