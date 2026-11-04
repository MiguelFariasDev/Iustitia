# Log Analytics + Application Insights workspace-based (ver ADR-036). Serilog e
# OpenTelemetry (UseAzureMonitor) exportam para cá quando
# ApplicationInsights:ConnectionString está no Key Vault.

resource "azurerm_log_analytics_workspace" "main" {
  name                = var.log_analytics_name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = var.retention_in_days
  tags                = var.tags
}

resource "azurerm_application_insights" "main" {
  name                = var.application_insights
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  retention_in_days   = var.retention_in_days

  # Nunca armazenar IP de cliente final (LGPD — dado pessoal sem finalidade aqui).
  ip_masking_enabled = true
  tags               = var.tags
}

# Workbooks = dashboards versionados (ver docs/devops/monitoring.md). O JSON usa
# KQL sobre as tabelas clássicas do App Insights (requests, customMetrics, ...).
resource "azurerm_application_insights_workbook" "operational" {
  name                = uuidv5("url", "${azurerm_application_insights.main.id}/workbooks/operational")
  location            = var.location
  resource_group_name = var.resource_group_name
  display_name        = "Advocacia — Operacional"
  source_id           = lower(azurerm_application_insights.main.id)
  category            = "workbook"
  data_json           = file("${path.module}/workbooks/operational.json")
  tags                = var.tags
}

resource "azurerm_application_insights_workbook" "business" {
  name                = uuidv5("url", "${azurerm_application_insights.main.id}/workbooks/business")
  location            = var.location
  resource_group_name = var.resource_group_name
  display_name        = "Advocacia — Negócio"
  source_id           = lower(azurerm_application_insights.main.id)
  category            = "workbook"
  data_json           = file("${path.module}/workbooks/business.json")
  tags                = var.tags
}
