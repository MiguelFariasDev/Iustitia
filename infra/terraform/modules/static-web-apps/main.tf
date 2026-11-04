# Frontend Next.js (web/) — deploy feito pelo workflow ci-frontend.yml com o
# token do próprio recurso, obtido em tempo de execução via OIDC
# (`az staticwebapp secrets list`), sem segredo estático no GitHub.

resource "azurerm_static_web_app" "main" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku_tier            = var.sku
  sku_size            = var.sku

  # Preview environments por PR só fazem sentido com integração nativa ao repo;
  # aqui o deploy é pelo pipeline, então ficam desligados.
  preview_environments_enabled = false

  tags = var.tags
}

# Certificado SSL gerenciado pelo Azure. Pré-requisito: CNAME
# <custom_domain> -> <default_host_name> criado no DNS do domínio (ver
# docs/devops/infrastructure.md, seção "Domínios").
resource "azurerm_static_web_app_custom_domain" "main" {
  count             = var.custom_domain == null ? 0 : 1
  static_web_app_id = azurerm_static_web_app.main.id
  domain_name       = var.custom_domain
  validation_type   = "cname-delegation"
}
