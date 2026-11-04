# ---------------------------------------------------------------------------
# Identidade do ambiente
# ---------------------------------------------------------------------------

variable "subscription_id" {
  description = "ID da assinatura Azure onde o ambiente é provisionado."
  type        = string
}

variable "environment" {
  description = "Nome curto do ambiente (dev, staging, prod) — entra no nome de todos os recursos."
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment deve ser dev, staging ou prod."
  }
}

variable "aspnetcore_environment" {
  description = "Valor de ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT nos containers (Development, Staging, Production)."
  type        = string
}

variable "location" {
  description = "Região principal dos recursos."
  type        = string
  default     = "brazilsouth"
}

variable "static_web_app_location" {
  description = "Região do Static Web App — o serviço não existe em Brazil South (regiões: westus2, centralus, eastus2, westeurope, eastasia)."
  type        = string
  default     = "eastus2"
}

variable "name_suffix" {
  description = "Sufixo opcional para nomes globalmente únicos (ACR, Storage, Key Vault) caso 'acradvocacia<env>' já esteja em uso por outra assinatura."
  type        = string
  default     = ""
}

variable "app_name" {
  description = "Nome da marca exibido na UI (Marca Desacoplada, ADR-023) — nunca entra em nome de recurso."
  type        = string
  default     = "Iustitia"
}

variable "extra_tags" {
  description = "Tags adicionais aplicadas a todos os recursos."
  type        = map(string)
  default     = {}
}

# ---------------------------------------------------------------------------
# Perfil de rede/custo (ver docs/devops/infrastructure.md, seção "Perfis")
# ---------------------------------------------------------------------------

variable "private_networking_enabled" {
  description = "Cria VNet, integra o Container Apps Environment e cria Private Endpoints (Key Vault, Service Bus, Redis, Blob). Exige service_bus_sku = Premium."
  type        = bool
  default     = false
}

variable "vnet_address_space" {
  description = "Espaço de endereços da VNet (usado só com private_networking_enabled)."
  type        = string
  default     = "10.40.0.0/16"
}

variable "pipeline_allowed_ips" {
  description = "IPs/CIDRs fixos com acesso ao firewall do Key Vault e do Storage quando private_networking_enabled (ex.: IP do escritório). Runners do GitHub são liberados dinamicamente pelos workflows."
  type        = list(string)
  default     = []
}

variable "front_door_enabled" {
  description = "Provisiona Azure Front Door (CDN + WAF) na frente da API."
  type        = bool
  default     = false
}

variable "front_door_sku" {
  description = "Standard_AzureFrontDoor (WAF só com regras customizadas) ou Premium_AzureFrontDoor (regras gerenciadas DRS + Bot Manager)."
  type        = string
  default     = "Standard_AzureFrontDoor"
}

variable "api_custom_domain" {
  description = "Domínio customizado da API no Front Door (ex.: api-staging.iustitia.com.br). null = só o domínio *.azurefd.net."
  type        = string
  default     = null
}

variable "frontend_custom_domain" {
  description = "Domínio customizado do frontend no Static Web App (ex.: app-staging.iustitia.com.br). null = só o domínio *.azurestaticapps.net."
  type        = string
  default     = null
}

# ---------------------------------------------------------------------------
# SKUs
# ---------------------------------------------------------------------------

variable "container_registry_sku" {
  description = "Basic (dev/staging) ou Standard/Premium (prod)."
  type        = string
  default     = "Basic"
}

variable "container_registry_georeplication_locations" {
  description = "Regiões de geo-replicação do ACR (exige SKU Premium). Vazio = sem geo-replicação."
  type        = list(string)
  default     = []
}

variable "service_bus_sku" {
  description = "Basic, Standard ou Premium. MassTransit usa tópicos, então o mínimo é Standard; Private Endpoint exige Premium."
  type        = string
  default     = "Standard"

  validation {
    condition     = !var.private_networking_enabled || var.service_bus_sku == "Premium"
    error_message = "private_networking_enabled exige service_bus_sku = \"Premium\" (Private Endpoint no Service Bus só existe no Premium)."
  }
}

variable "redis_sku" {
  description = "SKU do Azure Managed Redis (ex.: Balanced_B0, Balanced_B1)."
  type        = string
  default     = "Balanced_B0"
}

variable "redis_high_availability_enabled" {
  description = "Réplica de alta disponibilidade do Redis (dobra o custo)."
  type        = bool
  default     = false
}

variable "static_web_app_sku" {
  description = "Free (dev) ou Standard (staging/prod — domínio customizado com SLA, mais banda)."
  type        = string
  default     = "Free"
}

variable "log_retention_days" {
  description = "Retenção do Log Analytics/Application Insights."
  type        = number
  default     = 30
}

# ---------------------------------------------------------------------------
# Container Apps
# ---------------------------------------------------------------------------

variable "container_image_tag" {
  description = "Tag de imagem usada só na criação dos Container Apps. Depois disso quem troca a imagem é o pipeline (lifecycle.ignore_changes) — ver runbook de primeiro deploy em docs/devops/deploy.md."
  type        = string
  default     = "bootstrap"
}

variable "api" {
  description = "Recursos e escala do Container App da API."
  type = object({
    cpu                        = number
    memory                     = string
    min_replicas               = number
    max_replicas               = number
    http_concurrent_requests   = optional(number, 50)
    cpu_utilization_percent    = optional(number, 70)
    memory_utilization_percent = optional(number, 80)
  })
}

variable "worker" {
  description = "Recursos e escala do Container App do Worker (Hangfire + consumers)."
  type = object({
    cpu                        = number
    memory                     = string
    min_replicas               = number
    max_replicas               = number
    cpu_utilization_percent    = optional(number, 70)
    memory_utilization_percent = optional(number, 80)
  })
}

variable "frontend_origin" {
  description = "Origem do frontend liberada no CORS da API. null = usa o domínio padrão do Static Web App."
  type        = string
  default     = null
}

variable "service_bus_queues" {
  description = "Filas criadas pelo Terraform. As filas/tópicos dos consumers MassTransit são criados pelo próprio MassTransit (ConfigureEndpoints) e NÃO devem ser listados aqui — ver docs/devops/infrastructure.md."
  type        = list(string)
  default     = []
}

# ---------------------------------------------------------------------------
# Monitoramento
# ---------------------------------------------------------------------------

variable "alert_email_receivers" {
  description = "E-mails que recebem os alertas (plantão)."
  type        = list(string)
  default     = []
}

variable "alert_webhook_url" {
  description = "Webhook (Slack/Teams) para alertas. Sensível: passe via TF_VAR_alert_webhook_url, nunca em tfvars versionado."
  type        = string
  default     = null
  sensitive   = true
}

variable "monthly_budget_amount" {
  description = "Orçamento mensal do resource group (na moeda da assinatura). 0 = sem orçamento."
  type        = number
  default     = 0
}

variable "budget_start_date" {
  description = "Início do orçamento (primeiro dia de um mês, formato YYYY-MM-01T00:00:00Z)."
  type        = string
  default     = "2026-10-01T00:00:00Z"
}

# ---------------------------------------------------------------------------
# Backups
# ---------------------------------------------------------------------------

variable "backup_retention_days" {
  description = "Retenção por camada de backup (ver docs/compliance/backups.md)."
  type = object({
    daily   = number
    weekly  = number
    monthly = number
    storage = number
  })
  default = {
    daily   = 30
    weekly  = 365
    monthly = 1825
    storage = 365
  }
}

# ---------------------------------------------------------------------------
# Pipeline (GitHub Actions)
# ---------------------------------------------------------------------------

variable "github_actions_principal_id" {
  description = "Object ID da identidade (user-assigned, OIDC) usada pelos workflows do GitHub neste ambiente — criada por scripts/azure-bootstrap.sh. Recebe AcrPush, Key Vault Secrets Officer e Storage Blob Data Contributor."
  type        = string
}
