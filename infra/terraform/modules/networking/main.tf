# VNet usada só no perfil com rede privada (private_networking_enabled): o Container
# Apps Environment é integrado numa subnet dedicada e Key Vault, Service Bus, Redis e
# Blob Storage ganham Private Endpoints na subnet "private-endpoints", resolvidos via
# Private DNS Zones ligadas à VNet.

resource "azurerm_virtual_network" "main" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [var.address_space]
  tags                = var.tags
}

# Workload profiles environment exige no mínimo /27; usamos /23 para caber o
# crescimento de réplicas sem precisar recriar o environment (troca de subnet = recriação).
resource "azurerm_subnet" "container_apps" {
  name                 = "snet-container-apps"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [cidrsubnet(var.address_space, 7, 0)]

  delegation {
    name = "container-apps"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "private_endpoints" {
  name                              = "snet-private-endpoints"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.main.name
  address_prefixes                  = [cidrsubnet(var.address_space, 8, 2)]
  private_endpoint_network_policies = "Enabled"
}

locals {
  private_dns_zones = {
    vault      = "privatelink.vaultcore.azure.net"
    servicebus = "privatelink.servicebus.windows.net"
    redis      = "privatelink.redis.azure.net"
    blob       = "privatelink.blob.core.windows.net"
  }
}

resource "azurerm_private_dns_zone" "zones" {
  for_each            = local.private_dns_zones
  name                = each.value
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "links" {
  for_each             = local.private_dns_zones
  name                 = "link-${each.key}"
  private_dns_zone_id  = azurerm_private_dns_zone.zones[each.key].id
  virtual_network_id   = azurerm_virtual_network.main.id
  registration_enabled = false
  tags                 = var.tags
}
