# Azure Front Door na frente da API (CDN + WAF + TLS gerenciado). O frontend fica
# no Static Web App, que já tem CDN global própria — por isso só a API passa aqui.
# Ver docs/devops/infrastructure.md, seção "Front Door".

locals {
  is_premium = var.sku_name == "Premium_AzureFrontDoor"
}

resource "azurerm_cdn_frontdoor_profile" "main" {
  name                     = "afd-${var.project}-${var.environment}"
  resource_group_name      = var.resource_group_name
  sku_name                 = var.sku_name
  response_timeout_seconds = 60
  tags                     = var.tags
}

resource "azurerm_cdn_frontdoor_endpoint" "api" {
  name                     = "afd-${var.project}-api-${var.environment}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main.id
  tags                     = var.tags
}

resource "azurerm_cdn_frontdoor_origin_group" "api" {
  name                     = "api"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main.id
  session_affinity_enabled = false

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  # /health/live (não /health/ready): uma dependência fora do ar não deve tirar a
  # origem do Front Door — a API responde 503 com ProblemDetails, que é melhor que
  # o erro genérico do Front Door.
  health_probe {
    protocol            = "Https"
    path                = "/health/live"
    request_type        = "HEAD"
    interval_in_seconds = 60
  }
}

resource "azurerm_cdn_frontdoor_origin" "api" {
  name                           = "container-app"
  cdn_frontdoor_origin_group_id  = azurerm_cdn_frontdoor_origin_group.api.id
  enabled                        = true
  host_name                      = var.api_origin_hostname
  origin_host_header             = var.api_origin_hostname
  certificate_name_check_enabled = true
  https_port                     = 443
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_custom_domain" "api" {
  count                    = var.api_custom_domain == null ? 0 : 1
  name                     = replace(var.api_custom_domain, ".", "-")
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main.id
  host_name                = var.api_custom_domain

  tls {
    certificate_type = "ManagedCertificate"
    # Mínimo TLS 1.2 (TLS 1.3 é negociado sempre que o cliente suporta).
    minimum_version = "TLS12"
  }
}

resource "azurerm_cdn_frontdoor_route" "api" {
  name                            = "api"
  cdn_frontdoor_endpoint_id       = azurerm_cdn_frontdoor_endpoint.api.id
  cdn_frontdoor_origin_group_id   = azurerm_cdn_frontdoor_origin_group.api.id
  cdn_frontdoor_origin_ids        = [azurerm_cdn_frontdoor_origin.api.id]
  cdn_frontdoor_custom_domain_ids = azurerm_cdn_frontdoor_custom_domain.api[*].id
  patterns_to_match               = ["/*"]
  supported_protocols             = ["Http", "Https"]
  https_redirect_enabled          = true
  forwarding_protocol             = "HttpsOnly"
  link_to_default_domain          = true
  # Sem bloco cache: respostas da API nunca são cacheadas na borda (dados por tenant).
}

resource "azurerm_cdn_frontdoor_firewall_policy" "main" {
  name                = "waf${var.project}${var.environment}"
  resource_group_name = var.resource_group_name
  sku_name            = var.sku_name
  enabled             = true
  mode                = "Prevention"

  # Regras gerenciadas (OWASP/DRS + bots) só existem no Premium.
  dynamic "managed_rule" {
    for_each = local.is_premium ? [1] : []
    content {
      type    = "Microsoft_DefaultRuleSet"
      version = "2.1"
      action  = "Block"
    }
  }

  dynamic "managed_rule" {
    for_each = local.is_premium ? [1] : []
    content {
      type    = "Microsoft_BotManagerRuleSet"
      version = "1.1"
      action  = "Block"
    }
  }

  # Limite na borda contra força bruta de login (a API também tem rate limiting
  # próprio — ver RateLimitingConfiguration; este é a primeira barreira).
  custom_rule {
    name                           = "LoginRateLimit"
    enabled                        = true
    priority                       = 1
    type                           = "RateLimitRule"
    action                         = "Block"
    rate_limit_duration_in_minutes = 1
    rate_limit_threshold           = 30

    match_condition {
      match_variable = "RequestUri"
      operator       = "Contains"
      match_values   = ["/auth/login"]
    }
  }

  custom_rule {
    name                           = "GlobalRateLimit"
    enabled                        = true
    priority                       = 2
    type                           = "RateLimitRule"
    action                         = "Block"
    rate_limit_duration_in_minutes = 1
    rate_limit_threshold           = 1000

    match_condition {
      match_variable = "RequestUri"
      operator       = "Any"
      match_values   = []
    }
  }

  tags = var.tags
}

resource "azurerm_cdn_frontdoor_security_policy" "main" {
  name                     = "waf-api"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.main.id

  security_policies {
    firewall {
      cdn_frontdoor_firewall_policy_id = azurerm_cdn_frontdoor_firewall_policy.main.id

      association {
        patterns_to_match = ["/*"]

        domain {
          cdn_frontdoor_domain_id = azurerm_cdn_frontdoor_endpoint.api.id
        }

        dynamic "domain" {
          for_each = azurerm_cdn_frontdoor_custom_domain.api
          content {
            cdn_frontdoor_domain_id = domain.value.id
          }
        }
      }
    }
  }
}
