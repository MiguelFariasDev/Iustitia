# Consumidos pelos workflows (ver docs/devops/ci-cd.md) e pelo runbook de primeiro deploy.

output "resource_group_name" {
  value = data.azurerm_resource_group.main.name
}

output "container_registry_name" {
  value = module.container_registry.name
}

output "container_registry_login_server" {
  value = module.container_registry.login_server
}

output "container_apps_environment_default_domain" {
  value = module.container_apps.environment_default_domain
}

output "api_container_app_name" {
  value = module.container_apps.api_name
}

output "api_fqdn" {
  value = module.container_apps.api_fqdn
}

output "worker_container_app_name" {
  value = module.container_apps.worker_name
}

output "db_migrate_job_name" {
  value = module.container_apps.db_migrate_job_name
}

output "api_public_hostname" {
  description = "Hostname público da API: Front Door quando habilitado, senão o próprio Container App."
  value       = var.front_door_enabled ? coalesce(var.api_custom_domain, module.front_door[0].api_endpoint_hostname) : module.container_apps.api_fqdn
}

output "front_door_id" {
  value = var.front_door_enabled ? module.front_door[0].front_door_id : null
}

output "front_door_custom_domain_validation_token" {
  value = var.front_door_enabled ? module.front_door[0].api_custom_domain_validation_token : null
}

output "static_web_app_name" {
  value = module.static_web_app.name
}

output "static_web_app_default_hostname" {
  value = module.static_web_app.default_host_name
}

output "key_vault_name" {
  value = module.key_vault.name
}

output "key_vault_uri" {
  value = module.key_vault.vault_uri
}

output "backup_storage_account_name" {
  value = module.blob_storage.name
}

output "application_insights_app_id" {
  value = module.application_insights.application_insights_app_id
}

output "workload_identity_client_id" {
  value = azurerm_user_assigned_identity.workload.client_id
}
