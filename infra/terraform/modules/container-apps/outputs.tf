output "environment_id" {
  value = azurerm_container_app_environment.main.id
}

output "environment_default_domain" {
  value = azurerm_container_app_environment.main.default_domain
}

output "api_id" {
  value = azurerm_container_app.api.id
}

output "api_name" {
  value = azurerm_container_app.api.name
}

output "api_fqdn" {
  value = azurerm_container_app.api.ingress[0].fqdn
}

output "worker_id" {
  value = azurerm_container_app.worker.id
}

output "worker_name" {
  value = azurerm_container_app.worker.name
}

output "db_migrate_job_name" {
  value = azurerm_container_app_job.db_migrate.name
}
