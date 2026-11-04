# Azure Managed Redis (sucessor do Azure Cache for Redis, que está em
# aposentadoria). Usado como IDistributedCache (StackExchange.Redis) — ver
# BuildingBlocks.Infrastructure/DependencyInjection.cs, chave ConnectionStrings:Redis.

resource "azurerm_managed_redis" "main" {
  name                      = var.name
  location                  = var.location
  resource_group_name       = var.resource_group_name
  sku_name                  = var.sku_name
  high_availability_enabled = var.high_availability_enabled
  public_network_access     = var.private_endpoint_enabled ? "Disabled" : "Enabled"

  default_database {
    # O código atual autentica por chave (connection string). Ver "Pendências" em
    # docs/devops/infrastructure.md: migrar para Entra ID exige mudança no código.
    access_keys_authentication_enabled = true
    client_protocol                    = "Encrypted"
    clustering_policy                  = "EnterpriseCluster"
    eviction_policy                    = "VolatileLRU"
  }

  tags = var.tags
}

resource "azurerm_private_endpoint" "main" {
  count               = var.private_endpoint_enabled ? 1 : 0
  name                = "pe-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "psc-${var.name}"
    private_connection_resource_id = azurerm_managed_redis.main.id
    subresource_names              = ["redisEnterprise"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }

  tags = var.tags
}
