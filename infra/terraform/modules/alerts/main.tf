# Alertas (ver docs/devops/monitoring.md e docs/observability/alerts.md).
# Severidade Azure Monitor: 0 = crítico, 2 = warning, 3 = informativo.

resource "azurerm_monitor_action_group" "main" {
  name                = "ag-${var.project}-${var.environment}"
  resource_group_name = var.resource_group_name
  short_name          = substr("adv${var.environment}", 0, 12)
  tags                = var.tags

  dynamic "email_receiver" {
    for_each = var.email_receivers
    content {
      name                    = "email-${email_receiver.key}"
      email_address           = email_receiver.value
      use_common_alert_schema = true
    }
  }

  # Slack/Teams: o common alert schema chega como JSON genérico; para uma mensagem
  # formatada no Slack, aponte o webhook para um Workflow/Logic App (ver monitoring.md).
  dynamic "webhook_receiver" {
    for_each = var.webhook_url == null ? [] : [1]
    content {
      name                    = "chat-webhook"
      service_uri             = var.webhook_url
      use_common_alert_schema = true
    }
  }
}

# ---------------------------------------------------------------------------
# Críticos — consultas KQL sobre o Application Insights (métricas do Meter
# "Advocacia" chegam como customMetrics via OpenTelemetry/UseAzureMonitor).
# ---------------------------------------------------------------------------

locals {
  log_alerts = {
    outbox-pending = {
      description = "Outbox pendente acima de 1000 mensagens — publicação de eventos parada ou atrasada."
      severity    = 0
      frequency   = "PT5M"
      window      = "PT15M"
      query       = <<-KQL
        customMetrics
        | where name == "outbox.pending.count"
        | summarize pending = max(value)
      KQL
      measure     = "pending"
      threshold   = 1000
    }
    jobs-failed = {
      description = "Mais de 10 execuções de jobs recorrentes falharam na última hora."
      severity    = 0
      frequency   = "PT15M"
      window      = "PT1H"
      query       = <<-KQL
        customMetrics
        | where name == "jobs.failed.count"
        | summarize failures = sum(valueSum)
      KQL
      measure     = "failures"
      threshold   = 10
    }
    consumers-failed = {
      description = "Mais de 50 mensagens falharam nos consumers na última hora."
      severity    = 0
      frequency   = "PT15M"
      window      = "PT1H"
      query       = <<-KQL
        customMetrics
        | where name == "consumers.failed.count"
        | summarize failures = sum(valueSum)
      KQL
      measure     = "failures"
      threshold   = 50
    }
    http-error-rate = {
      description = "Taxa de erro HTTP (5xx) acima de 5% nos últimos 15 minutos."
      severity    = 2
      frequency   = "PT5M"
      window      = "PT15M"
      query       = <<-KQL
        requests
        | summarize total = count(), errors = countif(toint(resultCode) >= 500)
        | where total >= 20
        | extend errorRatePercent = 100.0 * errors / total
      KQL
      measure     = "errorRatePercent"
      threshold   = 5
    }
    # A especificação pede P95 de handlers MediatR > 2s. Histogramas exportados
    # pelo Azure Monitor chegam agregados (soma/contagem/mín/máx, sem percentis), e
    # a métrica handlers.duration ainda não é emitida pelo código (ver relatório da
    # Etapa 0.10). Até lá, o P95 é medido nas requisições HTTP, que envolvem os
    # handlers.
    latency-p95 = {
      description = "Latência P95 das requisições HTTP acima de 2s nos últimos 15 minutos."
      severity    = 2
      frequency   = "PT5M"
      window      = "PT15M"
      query       = <<-KQL
        requests
        | where name !has "/health"
        | summarize total = count(), p95 = percentile(duration, 95)
        | where total >= 20
      KQL
      measure     = "p95"
      threshold   = 2000
    }
  }
}

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "log_alerts" {
  for_each                = local.log_alerts
  name                    = "alert-${var.project}-${each.key}-${var.environment}"
  display_name            = "[${upper(var.environment)}] ${each.value.description}"
  description             = each.value.description
  location                = var.location
  resource_group_name     = var.resource_group_name
  scopes                  = [var.application_insights_id]
  severity                = each.value.severity
  evaluation_frequency    = each.value.frequency
  window_duration         = each.value.window
  auto_mitigation_enabled = true

  criteria {
    query                   = each.value.query
    metric_measure_column   = each.value.measure
    time_aggregation_method = "Maximum"
    operator                = "GreaterThan"
    threshold               = each.value.threshold

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.main.id]
  }

  tags = var.tags
}

# ---------------------------------------------------------------------------
# Dead letter queue (métrica nativa do Service Bus)
# ---------------------------------------------------------------------------

resource "azurerm_monitor_metric_alert" "dead_letter" {
  name                = "alert-${var.project}-dead-letter-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [var.service_bus_namespace_id]
  description         = "Mais de 100 mensagens em dead letter no Service Bus."
  severity            = 0
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.ServiceBus/namespaces"
    metric_name      = "DeadletteredMessages"
    aggregation      = "Maximum"
    operator         = "GreaterThan"
    threshold        = 100
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

# ---------------------------------------------------------------------------
# /health/ready — teste de disponibilidade a partir de 3 regiões
# ---------------------------------------------------------------------------

resource "azurerm_application_insights_standard_web_test" "ready" {
  name                    = "webtest-${var.project}-ready-${var.environment}"
  location                = var.location
  resource_group_name     = var.resource_group_name
  application_insights_id = var.application_insights_id
  enabled                 = true
  frequency               = 300
  timeout                 = 30
  retry_enabled           = true

  # IDs de localização do Application Insights (Brazil South, East US, Central US).
  geo_locations = ["latam-br-gru-edge", "us-va-ash-azr", "us-il-ch1-azr"]

  request {
    url       = var.api_health_url
    http_verb = "GET"
  }

  validation_rules {
    expected_status_code        = 200
    ssl_check_enabled           = true
    ssl_cert_remaining_lifetime = 7
  }

  tags = var.tags
}

resource "azurerm_monitor_metric_alert" "health_ready" {
  name                = "alert-${var.project}-health-ready-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [azurerm_application_insights_standard_web_test.ready.id, var.application_insights_id]
  description         = "/health/ready falhando em 2 ou mais regiões — alguma dependência crítica (Postgres, Redis, Service Bus, outbox) fora do ar."
  severity            = 0

  application_insights_web_test_location_availability_criteria {
    web_test_id           = azurerm_application_insights_standard_web_test.ready.id
    component_id          = var.application_insights_id
    failed_location_count = 2
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

# ---------------------------------------------------------------------------
# CPU / memória dos Container Apps (warning)
# ---------------------------------------------------------------------------

locals {
  resource_alerts = {
    api-cpu       = { scope = var.api_container_app_id, metric = "CpuPercentage", label = "CPU da API" }
    api-memory    = { scope = var.api_container_app_id, metric = "MemoryPercentage", label = "Memória da API" }
    worker-cpu    = { scope = var.worker_container_app_id, metric = "CpuPercentage", label = "CPU do Worker" }
    worker-memory = { scope = var.worker_container_app_id, metric = "MemoryPercentage", label = "Memória do Worker" }
  }
}

resource "azurerm_monitor_metric_alert" "resources" {
  for_each            = local.resource_alerts
  name                = "alert-${var.project}-${each.key}-${var.environment}"
  resource_group_name = var.resource_group_name
  scopes              = [each.value.scope]
  description         = "${each.value.label} acima de 80% (média de 15 minutos)."
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.App/containerApps"
    metric_name      = each.value.metric
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.main.id
  }

  tags = var.tags
}

# ---------------------------------------------------------------------------
# Custo (informativo)
# ---------------------------------------------------------------------------

resource "azurerm_consumption_budget_resource_group" "main" {
  count             = var.monthly_budget_amount > 0 ? 1 : 0
  name              = "budget-${var.project}-${var.environment}"
  resource_group_id = var.resource_group_id
  amount            = var.monthly_budget_amount
  time_grain        = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  notification {
    enabled        = true
    operator       = "GreaterThan"
    threshold      = 80
    threshold_type = "Actual"
    contact_emails = var.email_receivers
    contact_groups = [azurerm_monitor_action_group.main.id]
  }

  notification {
    enabled        = true
    operator       = "GreaterThan"
    threshold      = 100
    threshold_type = "Forecasted"
    contact_emails = var.email_receivers
    contact_groups = [azurerm_monitor_action_group.main.id]
  }

  lifecycle {
    ignore_changes = [time_period]
  }
}
