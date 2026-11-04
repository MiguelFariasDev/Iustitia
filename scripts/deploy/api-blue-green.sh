#!/usr/bin/env bash
# Deploy blue-green da API em Azure Container Apps (ADR-052):
#
#   1. fixa 100% do tráfego na revisão atual (azul) — a nova nasce com 0%
#   2. cria a revisão nova (verde) com a imagem nova
#   3. espera ela ficar saudável (startup/readiness probes)
#   4. roda os smoke tests na URL do label "green" (sem tráfego de usuário)
#   5. vira 100% do tráfego para a verde
#   6. observa App Insights por N minutos (erro > 10% / P95 > 5s => rollback)
#   7. desativa a azul (fica disponível para reativação em ~1 min — rollback.sh)
#
# Qualquer falha em 3–4 descarta a verde sem nunca ter recebido tráfego; falha em
# 6 devolve o tráfego para a azul (ainda ativa) em segundos.
#
#   ./scripts/deploy/api-blue-green.sh <env> <image-tag>
# Variáveis: APP_INSIGHTS_APP_ID (habilita a janela de observação),
#            WATCH_MINUTES (5), EXPECTED_ENVIRONMENT, SMOKE_TEST_EMAIL/PASSWORD.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
source "$DIR/lib.sh"

ENVIRONMENT="${1:?uso: $0 <env> <tag>}"
TAG="${2:?}"
RG="$(resource_group "$ENVIRONMENT")"
APP="$(api_app "$ENVIRONMENT")"
IMAGE="$(registry_name "$ENVIRONMENT").azurecr.io/advocacia-api:$TAG"
# Sufixo de revisão: único por execução, só [a-z0-9-], máx. ~20 caracteres.
SUFFIX="$(echo "${TAG}-${GITHUB_RUN_ATTEMPT:-$(date +%s)}" | tr '[:upper:]_.' '[:lower:]--' | tail -c 21 | sed 's/^-*//')"

BLUE="$(current_traffic_revision "$RG" "$APP")"
[[ -n "$BLUE" ]] || BLUE="$(latest_revision "$RG" "$APP")"
log "revisão azul (atual): $BLUE ($(revision_image "$RG" "$APP" "$BLUE"))"

# Remove o label de um deploy anterior interrompido.
az containerapp revision label remove --name "$APP" --resource-group "$RG" --label green --output none 2>/dev/null || true

log "fixando 100% do tráfego em $BLUE"
az containerapp ingress traffic set --name "$APP" --resource-group "$RG" --revision-weight "$BLUE=100" --output none

log "criando revisão verde com $IMAGE (sufixo $SUFFIX)"
az containerapp update --name "$APP" --resource-group "$RG" --image "$IMAGE" --revision-suffix "$SUFFIX" --output none
GREEN="$APP--$SUFFIX"

discard_green() {
  log "descartando $GREEN (nunca recebeu tráfego de usuário)"
  az containerapp revision label remove --name "$APP" --resource-group "$RG" --label green --output none 2>/dev/null || true
  az containerapp revision deactivate --name "$APP" --resource-group "$RG" --revision "$GREEN" --output none 2>/dev/null || true
}

if ! wait_revision_healthy "$RG" "$APP" "$GREEN" 600; then
  discard_green
  exit 1
fi

az containerapp revision label add --name "$APP" --resource-group "$RG" --label green --revision "$GREEN" --no-prompt --yes --output none
GREEN_URL="https://$APP---green.$(env_default_domain "$RG" "$APP")"
log "smoke tests na revisão verde: $GREEN_URL"
if ! "$DIR/../smoke-tests.sh" "$GREEN_URL"; then
  discard_green
  exit 1
fi

log "virando 100% do tráfego para $GREEN"
az containerapp ingress traffic set --name "$APP" --resource-group "$RG" --revision-weight "$GREEN=100" "$BLUE=0" --output none

if [[ -n "${APP_INSIGHTS_APP_ID:-}" ]]; then
  if ! "$DIR/watch.sh" "$APP_INSIGHTS_APP_ID" "$GREEN" "${WATCH_MINUTES:-5}"; then
    log "ROLLBACK AUTOMÁTICO: devolvendo 100% do tráfego para $BLUE"
    az containerapp ingress traffic set --name "$APP" --resource-group "$RG" --revision-weight "$BLUE=100" "$GREEN=0" --output none
    discard_green
    exit 1
  fi
else
  log "APP_INSIGHTS_APP_ID não definido — janela de observação pulada"
fi

# A azul é desativada (não custa réplicas) mas preservada: rollback.sh a reativa
# e devolve o tráfego. O nome dela fica registrado na tag previous-revision.
az containerapp revision label remove --name "$APP" --resource-group "$RG" --label green --output none 2>/dev/null || true
if [[ "$BLUE" != "$GREEN" ]]; then
  set_previous_revision "$RG" "$APP" "$BLUE"
  log "desativando $BLUE"
  az containerapp revision deactivate --name "$APP" --resource-group "$RG" --revision "$BLUE" --output none
fi

log "deploy concluído: $GREEN com 100% do tráfego"
echo "revision=$GREEN" >> "${GITHUB_OUTPUT:-/dev/null}"
echo "previous_revision=$BLUE" >> "${GITHUB_OUTPUT:-/dev/null}"
