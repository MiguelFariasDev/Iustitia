variable "project" { type = string }
variable "environment" { type = string }
variable "resource_group_name" { type = string }
variable "sku_name" { type = string }

variable "api_origin_hostname" {
  description = "FQDN do Container App da API."
  type        = string
}

variable "api_custom_domain" {
  type    = string
  default = null
}

variable "tags" { type = map(string) }
