#!/usr/bin/env bash
# Deploy do Worker (revisão única — sem tráfego HTTP, blue-green não se aplica).
# Se a revisão nova não ficar Running, volta automaticamente para a imagem anterior.
#
#   ./scripts/deploy/worker.sh <env> <image-tag>
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ENVIRONMENT="${1:?uso: $0 <env> <tag>}"
TAG="${2:?}"
RG="$(resource_group "$ENVIRONMENT")"
APP="$(worker_app "$ENVIRONMENT")"
IMAGE="$(registry_name "$ENVIRONMENT").azurecr.io/advocacia-worker:$TAG"
SUFFIX="$(echo "${TAG}-${GITHUB_RUN_ATTEMPT:-$(date +%s)}" | tr '[:upper:]_.' '[:lower:]--' | tail -c 21 | sed 's/^-*//')"

PREVIOUS="$(latest_revision "$RG" "$APP")"
PREVIOUS_IMAGE="$(revision_image "$RG" "$APP" "$PREVIOUS")"
log "revisão atual: $PREVIOUS ($PREVIOUS_IMAGE)"

az containerapp update --name "$APP" --resource-group "$RG" --image "$IMAGE" --revision-suffix "$SUFFIX" --output none
NEW="$APP--$SUFFIX"

# O Worker não tem probes; "saudável" = Running por 2 minutos sem reinício.
if wait_revision_healthy "$RG" "$APP" "$NEW" 600; then
  sleep 120
  restarts="$(az containerapp replica list --name "$APP" --resource-group "$RG" --revision "$NEW" \
    --query "sum([].properties.containers[].restartCount)" -o tsv 2>/dev/null || echo 0)"
  if [[ "${restarts:-0}" == "0" ]]; then
    log "worker em $NEW, sem reinícios"
    [[ "$PREVIOUS" != "$NEW" ]] && set_previous_revision "$RG" "$APP" "$PREVIOUS"
    echo "revision=$NEW" >> "${GITHUB_OUTPUT:-/dev/null}"
    exit 0
  fi
  log "$NEW reiniciou $restarts vez(es)"
fi

log "ROLLBACK AUTOMÁTICO do worker para $PREVIOUS_IMAGE"
az containerapp update --name "$APP" --resource-group "$RG" --image "$PREVIOUS_IMAGE" \
  --revision-suffix "rb-$(date +%s)" --output none
exit 1
