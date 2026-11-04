resource "azurerm_servicebus_namespace" "main" {
  name                          = var.name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  sku                           = var.sku
  capacity                      = var.sku == "Premium" ? 1 : 0
  premium_messaging_partitions  = var.sku == "Premium" ? 1 : 0
  minimum_tls_version           = "1.2"
  public_network_access_enabled = !var.private_endpoint_enabled
  tags                          = var.tags

  lifecycle {
    precondition {
      condition     = var.sku != "Basic"
      error_message = "MassTransit publica em tópicos — Service Bus Basic não tem tópicos. Use Standard ou Premium."
    }
    precondition {
      condition     = !var.private_endpoint_enabled || var.sku == "Premium"
      error_message = "Private Endpoint no Service Bus exige SKU Premium."
    }
  }
}

# Regra própria da aplicação (em vez da RootManageSharedAccessKey). Manage é
# necessário porque o MassTransit cria/atualiza a própria topologia (tópicos,
# subscriptions, filas dos consumers) ao subir. Ver "Pendências" em
# docs/devops/infrastructure.md: trocar por Managed Identity exige mudança no
# MassTransitConfiguration (Host(Uri) + DefaultAzureCredential).
resource "azurerm_servicebus_namespace_authorization_rule" "application" {
  name         = "advocacia-application"
  namespace_id = azurerm_servicebus_namespace.main.id
  listen       = true
  send         = true
  manage       = true
}

resource "azurerm_servicebus_queue" "queues" {
  for_each     = toset(var.queues)
  name         = each.value
  namespace_id = azurerm_servicebus_namespace.main.id

  max_delivery_count                   = 10
  dead_lettering_on_message_expiration = true
  lock_duration                        = "PT1M"
}

resource "azurerm_private_endpoint" "main" {
  count               = var.private_endpoint_enabled ? 1 : 0
  name                = "pe-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "psc-${var.name}"
    private_connection_resource_id = azurerm_servicebus_namespace.main.id
    subresource_names              = ["namespace"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }

  tags = var.tags
}
