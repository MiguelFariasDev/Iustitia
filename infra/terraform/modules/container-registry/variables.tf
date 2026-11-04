variable "name" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "sku" { type = string }

variable "georeplication_locations" {
  type    = list(string)
  default = []
}

variable "pull_principal_id" { type = string }
variable "push_principal_id" { type = string }

variable "tags" { type = map(string) }
