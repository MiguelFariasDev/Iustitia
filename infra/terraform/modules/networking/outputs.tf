output "vnet_id" {
  value = azurerm_virtual_network.main.id
}

output "container_apps_subnet_id" {
  value = azurerm_subnet.container_apps.id
}

output "private_endpoint_subnet_id" {
  value = azurerm_subnet.private_endpoints.id
}

output "private_dns_zone_ids" {
  description = "Mapa chave lógica (vault, servicebus, redis, blob) -> ID da Private DNS Zone."
  value       = { for key, zone in azurerm_private_dns_zone.zones : key => zone.id }
}
