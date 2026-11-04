# Key Vault com RBAC (sem access policies). Os workloads leem com a Managed Identity
# (Key Vault Secrets User) via AddAdvocaciaKeyVault/DefaultAzureCredential — ver
# docs/compliance/production-config.md. Backup de segredos = versionamento nativo +
# soft delete (90 dias) + purge protection em prod (ver docs/compliance/backups.md).

resource "azurerm_key_vault" "main" {
  name                       = var.name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true
  soft_delete_retention_days = 90
  purge_protection_enabled   = var.purge_protection_enabled

  # Com rede privada, o acesso público continua existindo mas atrás de firewall
  # (Deny por padrão): os workloads entram pelo Private Endpoint e os pipelines do
  # GitHub liberam o próprio IP temporariamente (scripts/ci-firewall.sh).
  public_network_access_enabled = true

  network_acls {
    default_action = var.private_endpoint_enabled ? "Deny" : "Allow"
    bypass         = "AzureServices"
    ip_rules       = var.allowed_ips
  }

  tags = var.tags

  lifecycle {
    # ip_rules temporárias adicionadas pelos workflows não devem virar drift.
    ignore_changes = [network_acls[0].ip_rules]
  }
}

resource "azurerm_role_assignment" "reader" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = var.reader_principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "writers" {
  for_each             = toset(var.writer_principal_ids)
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = each.value
}

# Role assignments no Azure levam até alguns minutos para propagar; sem essa espera
# o primeiro apply falha com 403 ao gravar os segredos logo em seguida.
resource "time_sleep" "rbac_propagation" {
  depends_on      = [azurerm_role_assignment.writers]
  create_duration = "90s"
}

resource "azurerm_key_vault_secret" "generated" {
  for_each     = nonsensitive(toset(keys(var.generated_secrets)))
  name         = each.value
  key_vault_id = azurerm_key_vault.main.id
  content_type = "generated-by-terraform"

  # Write-only: o valor é enviado ao Key Vault mas nunca gravado no state.
  # Para regravar (ex.: rotação da chave do Service Bus), incremente
  # generated_secrets_version.
  value_wo         = var.generated_secrets[each.value]
  value_wo_version = var.generated_secrets_version

  depends_on = [time_sleep.rbac_propagation]
}

resource "azurerm_key_vault_secret" "external" {
  for_each     = toset(var.external_secrets)
  name         = each.value
  key_vault_id = azurerm_key_vault.main.id
  content_type = "set-outside-terraform"

  # Placeholder: o valor real é gravado por scripts/keyvault-set-secrets.sh e o
  # Terraform nunca mais o toca (nem o lê para o state).
  value = "SET-ME"

  lifecycle {
    ignore_changes = [value, content_type, tags]
  }

  depends_on = [time_sleep.rbac_propagation]
}

resource "azurerm_private_endpoint" "main" {
  count               = var.private_endpoint_enabled ? 1 : 0
  name                = "pe-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "psc-${var.name}"
    private_connection_resource_id = azurerm_key_vault.main.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }

  tags = var.tags
}
