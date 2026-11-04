# Storage account dos backups (ver ADR-027 e docs/compliance/backups.md):
# pg_dump diário/semanal/mensal e cópia do Supabase Storage. Acesso só via Entra ID
# (shared keys desabilitadas) — os workflows usam OIDC + `--auth-mode login`.

resource "azurerm_storage_account" "main" {
  name                            = var.name
  location                        = var.location
  resource_group_name             = var.resource_group_name
  account_tier                    = "Standard"
  account_replication_type        = var.replication_type
  account_kind                    = "StorageV2"
  access_tier                     = "Hot"
  min_tls_version                 = "TLS1_2"
  https_traffic_only_enabled      = true
  shared_access_key_enabled       = false
  default_to_oauth_authentication = true
  allow_nested_items_to_be_public = false
  public_network_access           = "Enabled"

  blob_properties {
    # Versionamento + soft delete: um backup sobrescrito ou apagado por engano (ou por
    # credencial comprometida) continua recuperável pela janela abaixo.
    versioning_enabled = true

    delete_retention_policy {
      days = 30
    }

    container_delete_retention_policy {
      days = 30
    }
  }

  network_rules {
    default_action = var.private_endpoint_enabled ? "Deny" : "Allow"
    bypass         = ["AzureServices"]
    ip_rules       = var.allowed_ips
  }

  tags = var.tags

  lifecycle {
    # ip_rules temporárias adicionadas pelos workflows não devem virar drift.
    ignore_changes = [network_rules[0].ip_rules]
  }
}

locals {
  containers = ["db-backups-daily", "db-backups-weekly", "db-backups-monthly", "storage-backups"]
}

resource "azurerm_storage_container" "containers" {
  for_each              = toset(local.containers)
  name                  = each.value
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}

# hot -> cool -> archive -> expiração, por camada. Archive tem cobrança mínima de
# 180 dias e restauração em horas (reidratação) — por isso a camada diária nunca vai
# para archive (ver docs/runbooks/backup-restore.md).
resource "azurerm_storage_management_policy" "main" {
  storage_account_id = azurerm_storage_account.main.id

  rule {
    name    = "db-backups-daily"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["db-backups-daily/"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_creation_greater_than = 7
        delete_after_days_since_creation_greater_than       = var.retention_days.daily
      }
      version {
        delete_after_days_since_creation = 7
      }
    }
  }

  rule {
    name    = "db-backups-weekly"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["db-backups-weekly/"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_creation_greater_than    = 7
        tier_to_archive_after_days_since_creation_greater_than = 90
        delete_after_days_since_creation_greater_than          = var.retention_days.weekly
      }
      version {
        delete_after_days_since_creation = 7
      }
    }
  }

  rule {
    name    = "db-backups-monthly"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["db-backups-monthly/"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_creation_greater_than    = 7
        tier_to_archive_after_days_since_creation_greater_than = 90
        delete_after_days_since_creation_greater_than          = var.retention_days.monthly
      }
      version {
        delete_after_days_since_creation = 7
      }
    }
  }

  rule {
    name    = "storage-backups"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["storage-backups/"]
    }
    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than    = 30
        tier_to_archive_after_days_since_modification_greater_than = 90
        delete_after_days_since_modification_greater_than          = var.retention_days.storage
      }
      version {
        delete_after_days_since_creation = 30
      }
    }
  }
}

resource "azurerm_role_assignment" "writer" {
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.writer_principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_private_endpoint" "main" {
  count               = var.private_endpoint_enabled ? 1 : 0
  name                = "pe-${var.name}-blob"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "psc-${var.name}-blob"
    private_connection_resource_id = azurerm_storage_account.main.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [var.private_dns_zone_id]
  }

  tags = var.tags
}
