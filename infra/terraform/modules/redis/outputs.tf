output "id" {
  value = azurerm_managed_redis.main.id
}

output "connection_string" {
  description = "Formato StackExchange.Redis (TLS obrigatório)."
  value       = "${azurerm_managed_redis.main.hostname}:${azurerm_managed_redis.main.default_database[0].port},password=${azurerm_managed_redis.main.default_database[0].primary_access_key},ssl=True,abortConnect=False"
  sensitive   = true
}
