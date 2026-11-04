output "namespace_id" {
  value = azurerm_servicebus_namespace.main.id
}

output "application_connection_string" {
  value     = azurerm_servicebus_namespace_authorization_rule.application.primary_connection_string
  sensitive = true
}
