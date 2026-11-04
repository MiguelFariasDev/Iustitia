output "log_analytics_workspace_id" {
  value = azurerm_log_analytics_workspace.main.id
}

output "application_insights_id" {
  value = azurerm_application_insights.main.id
}

output "application_insights_app_id" {
  description = "App ID usado por `az monitor app-insights query` (janela de observação pós-deploy)."
  value       = azurerm_application_insights.main.app_id
}

output "connection_string" {
  value     = azurerm_application_insights.main.connection_string
  sensitive = true
}
