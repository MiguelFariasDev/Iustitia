#!/usr/bin/env bash
# Reinicia as revisões ativas da API e do Worker — necessário depois de trocar um
# segredo no Key Vault (a configuração é lida na inicialização).
#
#   ./scripts/deploy/restart.sh <env>
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ENVIRONMENT="${1:?uso: $0 <env>}"
RG="$(resource_group "$ENVIRONMENT")"

for APP in "$(api_app "$ENVIRONMENT")" "$(worker_app "$ENVIRONMENT")"; do
  for revision in $(az containerapp revision list --name "$APP" --resource-group "$RG" \
      --query "[?properties.active].name" -o tsv); do
    log "reiniciando $revision"
    az containerapp revision restart --name "$APP" --resource-group "$RG" --revision "$revision" --output none
  done
done
