terraform {
  # 1.11+ é necessário para atributos write-only (value_wo em azurerm_key_vault_secret),
  # usados para gravar segredos gerados no Key Vault sem persisti-los no state.
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.6"
    }
    time = {
      source  = "hashicorp/time"
      version = "~> 0.13"
    }
  }

  # Backend remoto no Azure Blob Storage (ver ADR-049). Os valores concretos
  # (storage account, container, key) vêm de environments/<env>/backend.hcl:
  #   terraform init -backend-config=environments/staging/backend.hcl
  # A storage account do state é criada fora do Terraform por
  # scripts/azure-bootstrap.sh (versionamento + soft delete habilitados).
  backend "azurerm" {
    use_azuread_auth = true
  }
}

provider "azurerm" {
  features {
    key_vault {
      # Nunca apagar definitivamente um Key Vault/segredo num destroy acidental —
      # soft delete + purge protection são a última linha de defesa.
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }

  subscription_id     = var.subscription_id
  storage_use_azuread = true
}
