output "profile_id" {
  value = azurerm_cdn_frontdoor_profile.main.id
}

output "front_door_id" {
  description = "GUID do perfil (header X-Azure-FDID) — usado para restringir o acesso direto ao Container App (ver Pendências em docs/devops/infrastructure.md)."
  value       = azurerm_cdn_frontdoor_profile.main.resource_guid
}

output "api_endpoint_hostname" {
  value = azurerm_cdn_frontdoor_endpoint.api.host_name
}

output "api_custom_domain_validation_token" {
  description = "Valor do TXT _dnsauth.<domínio> para validar o certificado gerenciado."
  value       = try(azurerm_cdn_frontdoor_custom_domain.api[0].validation_token, null)
}
