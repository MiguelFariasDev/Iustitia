variable "project" { type = string }
variable "environment" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "resource_group_id" { type = string }
variable "application_insights_id" { type = string }
variable "service_bus_namespace_id" { type = string }
variable "api_container_app_id" { type = string }
variable "worker_container_app_id" { type = string }
variable "api_health_url" { type = string }

variable "email_receivers" {
  type    = list(string)
  default = []
}

variable "webhook_url" {
  type      = string
  default   = null
  sensitive = true
}

variable "monthly_budget_amount" { type = number }
variable "budget_start_date" { type = string }
variable "tags" { type = map(string) }
