#!/usr/bin/env bash
# Rollback manual/automatizado (workflow rollback.yml ou à mão — ver
# docs/runbooks/rollback.md).
#
#   ./scripts/deploy/rollback.sh <env> api    [revisão]
#   ./scripts/deploy/rollback.sh <env> worker [revisão]
#
# Sem revisão explícita, volta para a registrada na tag previous-revision do
# Container App (a última estável antes do deploy atual).
#
# API: reativa a revisão alvo, espera ficar saudável, roda smoke tests nela e só
# então devolve 100% do tráfego. Worker: reimplanta a imagem da revisão alvo.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
source "$DIR/lib.sh"

ENVIRONMENT="${1:?uso: $0 <env> <api|worker> [revisão]}"
COMPONENT="${2:?}"
TARGET="${3:-}"
RG="$(resource_group "$ENVIRONMENT")"

resolve_target() {
  local rg="$1" app="$2" target
  target="$(get_previous_revision "$rg" "$app")"
  if [[ -z "$target" ]]; then
    log "sem revisão estável registrada (tag previous-revision). Revisões existentes:"
    az containerapp revision list --name "$app" --resource-group "$rg" --all \
      --query "sort_by(@, &properties.createdTime)[].{revisao:name, criada:properties.createdTime, ativa:properties.active, imagem:properties.template.containers[0].image}" -o table >&2
    log "informe a revisão alvo explicitamente."
    return 1
  fi
  echo "$target"
}

case "$COMPONENT" in
  api)
    APP="$(api_app "$ENVIRONMENT")"
    CURRENT="$(current_traffic_revision "$RG" "$APP")"
    [[ -n "$TARGET" ]] || TARGET="$(resolve_target "$RG" "$APP")"
    [[ "$TARGET" != "$CURRENT" ]] || { log "$TARGET já é a revisão com tráfego"; exit 1; }

    log "rollback da API: $CURRENT -> $TARGET ($(revision_image "$RG" "$APP" "$TARGET"))"
    az containerapp revision activate --name "$APP" --resource-group "$RG" --revision "$TARGET" --output none
    wait_revision_healthy "$RG" "$APP" "$TARGET" 600

    az containerapp revision label remove --name "$APP" --resource-group "$RG" --label green --output none 2>/dev/null || true
    az containerapp revision label add --name "$APP" --resource-group "$RG" --label green --revision "$TARGET" --no-prompt --yes --output none
    "$DIR/../smoke-tests.sh" "https://$APP---green.$(env_default_domain "$RG" "$APP")"

    az containerapp ingress traffic set --name "$APP" --resource-group "$RG" --revision-weight "$TARGET=100" "$CURRENT=0" --output none
    az containerapp revision label remove --name "$APP" --resource-group "$RG" --label green --output none 2>/dev/null || true
    # Evita que um segundo rollback "volte" para a revisão problemática.
    clear_previous_revision "$RG" "$APP"
    # A revisão problemática fica ativa com 0% por enquanto (útil para investigar
    # logs/réplicas); desative depois com: az containerapp revision deactivate.
    log "tráfego em $TARGET. Revisão anterior ($CURRENT) mantida com 0% para investigação."
    ;;
  worker)
    APP="$(worker_app "$ENVIRONMENT")"
    [[ -n "$TARGET" ]] || TARGET="$(resolve_target "$RG" "$APP")"
    IMAGE="$(revision_image "$RG" "$APP" "$TARGET")"
    log "rollback do worker para $IMAGE (de $TARGET)"
    az containerapp update --name "$APP" --resource-group "$RG" --image "$IMAGE" \
      --revision-suffix "rb-$(date +%s)" --output none
    wait_revision_healthy "$RG" "$APP" "$(latest_revision "$RG" "$APP")" 600
    clear_previous_revision "$RG" "$APP"
    ;;
  *)
    echo "componente inválido: $COMPONENT (api|worker)" >&2
    exit 1
    ;;
esac
