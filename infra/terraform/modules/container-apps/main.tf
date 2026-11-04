# Container Apps (ver ADR-051/052): API em modo de múltiplas revisões (blue-green
# por labels, conduzido pelo pipeline), Worker em revisão única e o job manual de
# migração do EF Core. Depois da criação, imagem e tráfego pertencem ao pipeline
# (lifecycle.ignore_changes) — o Terraform continua dono de escala, recursos,
# probes e variáveis de ambiente.

locals {
  api_name     = "ca-${var.project}-api-${var.environment}"
  worker_name  = "ca-${var.project}-worker-${var.environment}"
  migrate_name = "caj-${var.project}-db-migrate-${var.environment}"

  # Configuração comum aos dois hosts. Segredos NÃO entram aqui: a aplicação lê o
  # Key Vault diretamente (AddAdvocaciaKeyVault + DefaultAzureCredential, que usa a
  # identidade indicada em AZURE_CLIENT_ID).
  common_env = {
    ASPNETCORE_ENVIRONMENT = var.aspnetcore_environment
    DOTNET_ENVIRONMENT     = var.aspnetcore_environment
    APP_NAME               = var.app_name
    APP_ENV                = var.environment
    AppName                = var.app_name
    AppEnv                 = var.environment
    KeyVault__Uri          = var.key_vault_uri
    AZURE_CLIENT_ID        = var.identity_client_id
    # Não existe appsettings.Staging.json: sem isto, staging herdaria o transporte
    # RabbitMq do appsettings.json base.
    Messaging__Transport = "AzureServiceBus"
  }
}

resource "azurerm_container_app_environment" "main" {
  name                       = "cae-${var.project}-${var.environment}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = var.log_analytics_workspace_id
  infrastructure_subnet_id   = var.infrastructure_subnet_id

  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }

  tags = var.tags

  lifecycle {
    # Trocar de subnet recria o environment inteiro (e todas as apps).
    ignore_changes = [infrastructure_resource_group_name]
  }
}

resource "azurerm_container_app" "api" {
  name                         = local.api_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Multiple"
  max_inactive_revisions       = 5
  workload_profile_name        = "Consumption"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_id]
  }

  registry {
    server   = var.registry_login_server
    identity = var.identity_id
  }

  ingress {
    external_enabled           = true
    target_port                = 8080
    transport                  = "auto"
    allow_insecure_connections = false

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = var.api.min_replicas
    max_replicas = var.api.max_replicas

    container {
      name   = "api"
      image  = "${var.registry_login_server}/advocacia-api:${var.image_tag}"
      cpu    = var.api.cpu
      memory = var.api.memory

      dynamic "env" {
        for_each = merge(local.common_env, {
          ASPNETCORE_URLS         = "http://+:8080"
          Cors__AllowedOrigins__0 = var.cors_allowed_origin
        })
        content {
          name  = env.key
          value = env.value
        }
      }

      startup_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/live"
        interval_seconds        = 5
        failure_count_threshold = 24 # até 2 min para subir (leitura do Key Vault + conexão com dependências)
        timeout                 = 3
      }

      liveness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/live"
        interval_seconds        = 15
        failure_count_threshold = 3
        timeout                 = 3
      }

      readiness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/ready"
        interval_seconds        = 10
        failure_count_threshold = 3
        success_count_threshold = 1
        timeout                 = 5
      }
    }

    # Container Apps escala por requisições concorrentes (não por minuto): 1000
    # req/min ≈ 17 req/s; com ~100–300 ms por requisição isso dá poucas dezenas de
    # requisições simultâneas por réplica — daí o padrão de 50.
    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = tostring(var.api.http_concurrent_requests)
    }

    custom_scale_rule {
      name             = "cpu"
      custom_rule_type = "cpu"
      metadata = {
        type  = "Utilization"
        value = tostring(var.api.cpu_utilization_percent)
      }
    }

    custom_scale_rule {
      name             = "memory"
      custom_rule_type = "memory"
      metadata = {
        type  = "Utilization"
        value = tostring(var.api.memory_utilization_percent)
      }
    }
  }

  tags = var.tags

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
      template[0].revision_suffix,
      ingress[0].traffic_weight,
      # Última revisão estável, gravada pelo pipeline para o rollback (ADR-052).
      tags["previous-revision"],
    ]
  }
}

resource "azurerm_container_app" "worker" {
  name                         = local.worker_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  max_inactive_revisions       = 5
  workload_profile_name        = "Consumption"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_id]
  }

  registry {
    server   = var.registry_login_server
    identity = var.identity_id
  }

  # Sem ingress: o Worker não expõe HTTP (Hangfire server + consumers MassTransit).
  template {
    min_replicas = var.worker.min_replicas
    max_replicas = var.worker.max_replicas

    # Hangfire/MassTransit fazem shutdown gracioso ao receber SIGTERM — damos tempo
    # para o job/mensagem em andamento terminar antes do kill.
    termination_grace_period_seconds = 60

    container {
      name   = "worker"
      image  = "${var.registry_login_server}/advocacia-worker:${var.image_tag}"
      cpu    = var.worker.cpu
      memory = var.worker.memory

      dynamic "env" {
        for_each = local.common_env
        content {
          name  = env.key
          value = env.value
        }
      }
    }

    custom_scale_rule {
      name             = "cpu"
      custom_rule_type = "cpu"
      metadata = {
        type  = "Utilization"
        value = tostring(var.worker.cpu_utilization_percent)
      }
    }

    custom_scale_rule {
      name             = "memory"
      custom_rule_type = "memory"
      metadata = {
        type  = "Utilization"
        value = tostring(var.worker.memory_utilization_percent)
      }
    }
  }

  tags = var.tags

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
      template[0].revision_suffix,
      tags["previous-revision"],
    ]
  }
}

# Job manual de migração do EF Core (bundle gerado por
# infra/docker/migrator.Dockerfile). Disparado pelo pipeline ANTES de trocar a
# imagem da API/Worker — ver docs/runbooks/database-migration.md. Conecta como o
# dono do schema (ConnectionStrings--Migrations), nunca como app_user.
resource "azurerm_container_app_job" "db_migrate" {
  name                         = local.migrate_name
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  workload_profile_name        = "Consumption"
  replica_timeout_in_seconds   = 1800
  replica_retry_limit          = 0

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [var.identity_id]
  }

  registry {
    server   = var.registry_login_server
    identity = var.identity_id
  }

  secret {
    name                = "migrations-connection"
    key_vault_secret_id = var.migrations_secret_id
    identity            = var.identity_id
  }

  template {
    container {
      name   = "migrator"
      image  = "${var.registry_login_server}/advocacia-migrator:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ADVOCACIA_DB_CONNECTION"
        secret_name = "migrations-connection"
      }
    }
  }

  tags = var.tags

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }
}
