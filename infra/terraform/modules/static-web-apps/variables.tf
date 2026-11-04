variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "sku" { type = string }

variable "custom_domain" {
  type    = string
  default = null
}

variable "tags" { type = map(string) }
