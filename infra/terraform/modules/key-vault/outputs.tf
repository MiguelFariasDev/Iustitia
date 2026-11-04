output "id" {
  value = azurerm_key_vault.main.id
}

output "name" {
  value = azurerm_key_vault.main.name
}

output "vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "external_secret_versionless_ids" {
  description = "IDs sem versão (sempre a versão mais recente) — usados como referência de segredo no Container App Job de migração."
  value       = { for name, secret in azurerm_key_vault_secret.external : name => secret.versionless_id }
}
