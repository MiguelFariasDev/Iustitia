variable "log_analytics_name" { type = string }
variable "application_insights" { type = string }
variable "location" { type = string }
variable "resource_group_name" { type = string }
variable "retention_in_days" { type = number }
variable "tags" { type = map(string) }
