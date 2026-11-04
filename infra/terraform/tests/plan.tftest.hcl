# Testes de plan com providers mockados — rodam sem credencial Azure (CI e local):
#   terraform init -backend=false && terraform test
# Validam a lógica da composição por ambiente (perfis, counts, preconditions),
# não o comportamento da API do Azure (isso só um `terraform plan` real verifica).

mock_provider "azurerm" {
  mock_data "azurerm_client_config" {
    defaults = {
      tenant_id       = "00000000-0000-0000-0000-000000000001"
      object_id       = "00000000-0000-0000-0000-000000000002"
      subscription_id = "00000000-0000-0000-0000-000000000003"
    }
  }

  mock_data "azurerm_resource_group" {
    defaults = {
      id       = "/subscriptions/00000000-0000-0000-0000-000000000003/resourceGroups/rg-advocacia-test"
      location = "brazilsouth"
    }
  }

  mock_resource "azurerm_user_assigned_identity" {
    defaults = {
      id           = "/subscriptions/00000000-0000-0000-0000-000000000003/resourceGroups/rg-advocacia-test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id-advocacia-test"
      principal_id = "00000000-0000-0000-0000-000000000004"
      client_id    = "00000000-0000-0000-0000-000000000005"
    }
  }
}

mock_provider "time" {}

variables {
  subscription_id             = "00000000-0000-0000-0000-000000000003"
  github_actions_principal_id = "00000000-0000-0000-0000-000000000006"
}

run "staging_economic_profile" {
  command = plan

  variables {
    environment                = "staging"
    aspnetcore_environment     = "Staging"
    private_networking_enabled = false
    front_door_enabled         = true
    front_door_sku             = "Standard_AzureFrontDoor"
    service_bus_sku            = "Standard"
    api                        = { cpu = 0.5, memory = "1Gi", min_replicas = 1, max_replicas = 5 }
    worker                     = { cpu = 0.5, memory = "1Gi", min_replicas = 1, max_replicas = 3 }
  }

  assert {
    condition     = length(module.networking) == 0
    error_message = "Staging econômico não deve criar VNet."
  }

  assert {
    condition     = length(module.front_door) == 1
    error_message = "Staging deve ter Front Door."
  }

  assert {
    condition     = module.container_registry.name == "acradvocaciastaging"
    error_message = "Nome do ACR deve seguir acradvocacia<env> (Marca Desacoplada)."
  }
}

run "prod_full_profile" {
  command = plan

  variables {
    environment                = "prod"
    aspnetcore_environment     = "Production"
    private_networking_enabled = true
    front_door_enabled         = true
    front_door_sku             = "Premium_AzureFrontDoor"
    service_bus_sku            = "Premium"
    api_custom_domain          = "api.iustitia.com.br"
    frontend_custom_domain     = "app.iustitia.com.br"
    api                        = { cpu = 1.0, memory = "2Gi", min_replicas = 2, max_replicas = 10 }
    worker                     = { cpu = 1.0, memory = "2Gi", min_replicas = 1, max_replicas = 5 }
    monthly_budget_amount      = 2500
  }

  assert {
    condition     = length(module.networking) == 1
    error_message = "Produção deve criar VNet + Private Endpoints."
  }
}

run "private_networking_requires_premium_service_bus" {
  command = plan

  variables {
    environment                = "prod"
    aspnetcore_environment     = "Production"
    private_networking_enabled = true
    service_bus_sku            = "Standard"
    api                        = { cpu = 1.0, memory = "2Gi", min_replicas = 2, max_replicas = 10 }
    worker                     = { cpu = 1.0, memory = "2Gi", min_replicas = 1, max_replicas = 5 }
  }

  expect_failures = [var.service_bus_sku]
}

run "rejects_unknown_environment" {
  command = plan

  variables {
    environment            = "qa"
    aspnetcore_environment = "Staging"
    api                    = { cpu = 0.5, memory = "1Gi", min_replicas = 1, max_replicas = 5 }
    worker                 = { cpu = 0.5, memory = "1Gi", min_replicas = 1, max_replicas = 3 }
  }

  expect_failures = [var.environment]
}
