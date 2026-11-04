# Composição do ambiente Azure do produto (ver ADR-049 e docs/devops/infrastructure.md).
# Todos os nomes de recurso usam "advocacia" — a marca (Iustitia) só aparece em
# configuração (APP_NAME) e em domínios customizados (ADR-023).

locals {
  project = "advocacia"
  env     = var.environment

  # Nomes globalmente únicos, sem hífen (ACR/Storage só aceitam [a-z0-9]).
  compact_suffix = "${local.env}${var.name_suffix}"

  tags = merge({
    Environment = local.env
    Project     = "Advocacia"
    ManagedBy   = "Terraform"
  }, var.extra_tags)
}

data "azurerm_client_config" "current" {}

# O resource group é criado por scripts/azure-bootstrap.sh, não aqui: a identidade
# OIDC do GitHub Actions recebe permissões com escopo neste RG (menor privilégio —
# ver ADR-049), então ele precisa existir antes do primeiro `terraform apply`.
data "azurerm_resource_group" "main" {
  name = "rg-${local.project}-${local.env}"
}

# Identidade única dos workloads (API, Worker, job de migração): AcrPull no ACR e
# Key Vault Secrets User no Key Vault. User-assigned (não system-assigned) para que as
# role assignments existam ANTES da primeira revisão subir — com system-assigned a
# primeira revisão falharia ao puxar a imagem/ler segredos.
resource "azurerm_user_assigned_identity" "workload" {
  name                = "id-${local.project}-${local.env}"
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  tags                = local.tags
}

module "networking" {
  source = "./modules/networking"
  count  = var.private_networking_enabled ? 1 : 0

  name                = "vnet-${local.project}-${local.env}"
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  address_space       = var.vnet_address_space
  tags                = local.tags
}

locals {
  private_endpoint_subnet_id = var.private_networking_enabled ? module.networking[0].private_endpoint_subnet_id : null
  private_dns_zone_ids       = var.private_networking_enabled ? module.networking[0].private_dns_zone_ids : {}
}

module "application_insights" {
  source = "./modules/application-insights"

  log_analytics_name   = "log-${local.project}-${local.env}"
  application_insights = "ai-${local.project}-${local.env}"
  location             = var.location
  resource_group_name  = data.azurerm_resource_group.main.name
  retention_in_days    = var.log_retention_days
  tags                 = local.tags
}

module "container_registry" {
  source = "./modules/container-registry"

  name                     = "acr${local.project}${local.compact_suffix}"
  location                 = var.location
  resource_group_name      = data.azurerm_resource_group.main.name
  sku                      = var.container_registry_sku
  georeplication_locations = var.container_registry_georeplication_locations
  pull_principal_id        = azurerm_user_assigned_identity.workload.principal_id
  push_principal_id        = var.github_actions_principal_id
  tags                     = local.tags
}

module "service_bus" {
  source = "./modules/service-bus"

  name                       = "sb-${local.project}-${local.env}"
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  sku                        = var.service_bus_sku
  queues                     = var.service_bus_queues
  private_endpoint_enabled   = var.private_networking_enabled
  private_endpoint_subnet_id = local.private_endpoint_subnet_id
  private_dns_zone_id        = lookup(local.private_dns_zone_ids, "servicebus", null)
  tags                       = local.tags
}

module "redis" {
  source = "./modules/redis"

  name                       = "redis-${local.project}-${local.env}"
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  sku_name                   = var.redis_sku
  high_availability_enabled  = var.redis_high_availability_enabled
  private_endpoint_enabled   = var.private_networking_enabled
  private_endpoint_subnet_id = local.private_endpoint_subnet_id
  private_dns_zone_id        = lookup(local.private_dns_zone_ids, "redis", null)
  tags                       = local.tags
}

module "key_vault" {
  source = "./modules/key-vault"

  name                       = "kv-${local.project}-${local.compact_suffix}"
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  purge_protection_enabled   = local.env == "prod"
  reader_principal_id        = azurerm_user_assigned_identity.workload.principal_id
  writer_principal_ids       = distinct([data.azurerm_client_config.current.object_id, var.github_actions_principal_id])
  private_endpoint_enabled   = var.private_networking_enabled
  private_endpoint_subnet_id = local.private_endpoint_subnet_id
  private_dns_zone_id        = lookup(local.private_dns_zone_ids, "vault", null)
  allowed_ips                = var.pipeline_allowed_ips

  # Segredos que o próprio Terraform conhece (gerados pelos recursos acima). Gravados
  # com value_wo (write-only): o valor nunca vai para o state do segredo. Os nomes
  # seguem a convenção do provider de configuração do .NET ("--" vira ":") e batem
  # 1:1 com as chaves já usadas no código (docs/compliance/production-config.md).
  generated_secrets = {
    "Messaging--AzureServiceBusConnectionString" = module.service_bus.application_connection_string
    "ConnectionStrings--Redis"                   = module.redis.connection_string
    "ApplicationInsights--ConnectionString"      = module.application_insights.connection_string
  }

  # Segredos externos (Supabase, Anthropic, CNJ): o Terraform só cria o segredo com um
  # placeholder e ignora o valor dali em diante — o valor real é gravado fora do
  # Terraform por scripts/keyvault-set-secrets.sh e nunca passa pelo state.
  external_secrets = [
    "ConnectionStrings--DefaultConnection",
    "ConnectionStrings--Migrations",
    "Supabase--Url",
    "Supabase--AnonKey",
    "Supabase--ServiceRoleKey",
    "Anthropic--ApiKey",
    "Cnj--ApiKey",
  ]

  tags = local.tags
}

module "blob_storage" {
  source = "./modules/blob-storage"

  name                       = "st${local.project}${local.compact_suffix}"
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  replication_type           = local.env == "prod" ? "GRS" : "LRS"
  retention_days             = var.backup_retention_days
  writer_principal_id        = var.github_actions_principal_id
  private_endpoint_enabled   = var.private_networking_enabled
  private_endpoint_subnet_id = local.private_endpoint_subnet_id
  private_dns_zone_id        = lookup(local.private_dns_zone_ids, "blob", null)
  allowed_ips                = var.pipeline_allowed_ips
  tags                       = local.tags
}

module "static_web_app" {
  source = "./modules/static-web-apps"

  name                = "swa-${local.project}-${local.env}"
  location            = var.static_web_app_location
  resource_group_name = data.azurerm_resource_group.main.name
  sku                 = var.static_web_app_sku
  custom_domain       = var.frontend_custom_domain
  tags                = local.tags
}

module "container_apps" {
  source = "./modules/container-apps"

  project                    = local.project
  environment                = local.env
  aspnetcore_environment     = var.aspnetcore_environment
  app_name                   = var.app_name
  location                   = var.location
  resource_group_name        = data.azurerm_resource_group.main.name
  log_analytics_workspace_id = module.application_insights.log_analytics_workspace_id
  infrastructure_subnet_id   = var.private_networking_enabled ? module.networking[0].container_apps_subnet_id : null

  identity_id           = azurerm_user_assigned_identity.workload.id
  identity_client_id    = azurerm_user_assigned_identity.workload.client_id
  registry_login_server = module.container_registry.login_server
  image_tag             = var.container_image_tag
  key_vault_uri         = module.key_vault.vault_uri
  migrations_secret_id  = module.key_vault.external_secret_versionless_ids["ConnectionStrings--Migrations"]
  cors_allowed_origin   = coalesce(var.frontend_origin, "https://${module.static_web_app.default_host_name}")

  api    = var.api
  worker = var.worker
  tags   = local.tags

  # As role assignments (AcrPull/Key Vault Secrets User) precisam existir antes da
  # primeira revisão tentar puxar a imagem e ler o Key Vault.
  depends_on = [module.container_registry, module.key_vault]
}

module "front_door" {
  source = "./modules/front-door"
  count  = var.front_door_enabled ? 1 : 0

  project             = local.project
  environment         = local.env
  resource_group_name = data.azurerm_resource_group.main.name
  sku_name            = var.front_door_sku
  api_origin_hostname = module.container_apps.api_fqdn
  api_custom_domain   = var.api_custom_domain
  tags                = local.tags
}

module "alerts" {
  source = "./modules/alerts"

  project                  = local.project
  environment              = local.env
  location                 = var.location
  resource_group_name      = data.azurerm_resource_group.main.name
  resource_group_id        = data.azurerm_resource_group.main.id
  application_insights_id  = module.application_insights.application_insights_id
  service_bus_namespace_id = module.service_bus.namespace_id
  api_container_app_id     = module.container_apps.api_id
  worker_container_app_id  = module.container_apps.worker_id
  api_health_url           = "https://${module.container_apps.api_fqdn}/health/ready"
  email_receivers          = var.alert_email_receivers
  webhook_url              = var.alert_webhook_url
  monthly_budget_amount    = var.monthly_budget_amount
  budget_start_date        = var.budget_start_date
  tags                     = local.tags
}
