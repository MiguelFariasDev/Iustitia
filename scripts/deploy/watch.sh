#!/usr/bin/env bash
# Janela de observação pós-deploy: consulta o Application Insights a cada minuto
# e falha (exit 1) se a revisão nova passar dos limites de rollback automático
# (ADR-052): taxa de erro > 10% ou latência P95 > 5s, com volume mínimo de
# requisições para a estatística fazer sentido.
#
#   ./scripts/deploy/watch.sh <app-insights-app-id> <revisão> [minutos=5]
set -euo pipefail
source "$(dirname "$0")/lib.sh"

APP_ID="${1:?uso: $0 <app-insights-app-id> <revisão> [minutos]}"
REVISION="${2:?}"
MINUTES="${3:-5}"
MAX_ERROR_RATE="${MAX_ERROR_RATE_PERCENT:-10}"
MAX_P95_MS="${MAX_P95_MS:-5000}"
MIN_REQUESTS="${MIN_REQUESTS:-20}"
START="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

# cloud_RoleInstance = service.instance.id = nome da réplica, que começa com o nome
# da revisão (ex.: ca-advocacia-api-staging--a1b2c3d-5f7c9-xyz).
QUERY="requests
| where timestamp >= datetime($START) and cloud_RoleInstance startswith '$REVISION' and name !has '/health'
| summarize total = count(), errors = countif(toint(resultCode) >= 500), p95 = percentile(duration, 95)"

for minute in $(seq 1 "$MINUTES"); do
  sleep 60
  read -r total errors p95 < <(az monitor app-insights query --app "$APP_ID" --analytics-query "$QUERY" \
    --query "tables[0].rows[0]" -o tsv | tr '\n' ' ')
  total="${total:-0}"; errors="${errors:-0}"; p95="${p95:-0}"
  rate=0
  (( total > 0 )) && rate=$(( errors * 100 / total ))
  log "minuto $minute/$MINUTES: requisições=$total erros=$errors (${rate}%) p95=${p95%.*}ms"

  if (( total >= MIN_REQUESTS )); then
    if (( rate > MAX_ERROR_RATE )); then
      log "taxa de erro ${rate}% > ${MAX_ERROR_RATE}% — rollback"
      exit 1
    fi
    if (( ${p95%.*} > MAX_P95_MS )); then
      log "P95 ${p95%.*}ms > ${MAX_P95_MS}ms — rollback"
      exit 1
    fi
  fi
done

log "janela de observação concluída sem violar limites"
