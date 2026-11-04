# ACR por ambiente (ver ADR-050): staging recebe as imagens do CI; prod só recebe
# imagens promovidas de staging via `az acr import` (a mesma imagem testada em
# staging é a que vai para produção — nunca um rebuild).

resource "azurerm_container_registry" "main" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku

  # Admin user desabilitado de propósito (divergência consciente da especificação
  # original — ver ADR-050): Container Apps puxam com Managed Identity (AcrPull) e o
  # GitHub Actions empurra via OIDC (AcrPush). Nenhuma senha de registry existe.
  admin_enabled = false

  dynamic "georeplications" {
    for_each = var.georeplication_locations
    content {
      location                        = georeplications.value
      global_endpoint_routing_enabled = true
      zone_redundancy_enabled         = false
    }
  }

  tags = var.tags
}

resource "azurerm_role_assignment" "pull" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPull"
  principal_id         = var.pull_principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "push" {
  scope                = azurerm_container_registry.main.id
  role_definition_name = "AcrPush"
  principal_id         = var.push_principal_id
  principal_type       = "ServicePrincipal"
}

# Retenção (últimas 10 imagens por repositório) é aplicada pelo pipeline, logo após
# cada push, por scripts/acr-retention.sh — e não por uma ACR Task agendada: o
# script nunca apaga uma imagem ainda referenciada por uma revisão ativa (ex.: prod
# atrasado em relação a staging) e não depende de ACR Tasks, que não estão
# disponíveis em todo tipo de assinatura.
