#!/usr/bin/env bash
# Funções compartilhadas pelos scripts de deploy/rollback (source, não executar).
# Convenção de nomes: ver infra/terraform (Marca Desacoplada — "advocacia").

resource_group() { echo "rg-advocacia-$1"; }
api_app() { echo "ca-advocacia-api-$1"; }
worker_app() { echo "ca-advocacia-worker-$1"; }
migrate_job() { echo "caj-advocacia-db-migrate-$1"; }
registry_name() { echo "${ACR_NAME:-acradvocacia$1}"; }

log() { printf '[%s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }

# Revisão que hoje recebe 100% do tráfego (a "azul").
current_traffic_revision() {
  local rg="$1" app="$2"
  az containerapp ingress traffic show --name "$app" --resource-group "$rg" \
    --query "max_by([?weight > \`0\`], &weight).revisionName" -o tsv 2>/dev/null
}

latest_revision() {
  az containerapp show --name "$2" --resource-group "$1" --query properties.latestRevisionName -o tsv
}

revision_image() {
  az containerapp revision show --name "$2" --resource-group "$1" --revision "$3" \
    --query "properties.template.containers[0].image" -o tsv
}

# Espera a revisão ficar provisionada e saudável (probes de startup/readiness ok).
wait_revision_healthy() {
  local rg="$1" app="$2" revision="$3" timeout="${4:-600}" elapsed=0 state health running
  while (( elapsed < timeout )); do
    read -r state health running < <(az containerapp revision show --name "$app" --resource-group "$rg" \
      --revision "$revision" --query "[properties.provisioningState, properties.healthState, properties.runningState]" -o tsv | tr '\n' ' ')
    log "revisão $revision: provisioning=$state health=$health running=$running"
    case "$state" in
      Failed) return 1 ;;
      Provisioned)
        [[ "$health" == "Healthy" ]] && return 0
        [[ "$health" == "Unhealthy" && "$running" =~ ^(Failed|Degraded)$ ]] && return 1
        # Sem ingress/probes (Worker), "None" + Running já é o estado final.
        [[ "$health" == "None" && "$running" == "Running" ]] && return 0
        ;;
    esac
    sleep 15
    elapsed=$((elapsed + 15))
  done
  log "timeout esperando $revision ficar saudável"
  return 1
}

env_default_domain() {
  az containerapp env show --ids "$(az containerapp show --name "$2" --resource-group "$1" \
    --query properties.environmentId -o tsv)" --query properties.defaultDomain -o tsv
}

# A revisão estável anterior ao último deploy fica numa tag do próprio Container
# App (o Terraform ignora essa tag). Labels não servem para isso: só valem para
# revisões ativas, e a revisão anterior é desativada ao fim do deploy.
set_previous_revision() {
  local rg="$1" app="$2" revision="$3" id
  id="$(az containerapp show --name "$app" --resource-group "$rg" --query id -o tsv)"
  az tag update --resource-id "$id" --operation Merge --tags "previous-revision=$revision" --output none
}

get_previous_revision() {
  az containerapp show --name "$2" --resource-group "$1" --query 'tags."previous-revision"' -o tsv 2>/dev/null
}

clear_previous_revision() {
  local rg="$1" app="$2" id
  id="$(az containerapp show --name "$app" --resource-group "$rg" --query id -o tsv)"
  az tag update --resource-id "$id" --operation Delete --tags "previous-revision" --output none 2>/dev/null || true
}
