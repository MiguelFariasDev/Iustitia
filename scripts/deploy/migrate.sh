#!/usr/bin/env bash
# Aplica as migrations do EF Core rodando o Container App Job de migração com a
# imagem do migrator da versão que está sendo implantada. Tem que rodar ANTES de
# trocar a imagem da API/Worker (migrations sempre retrocompatíveis com a versão
# anterior — ver docs/runbooks/database-migration.md).
#
#   ./scripts/deploy/migrate.sh <env> <image-tag> [target-migration]
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ENVIRONMENT="${1:?uso: $0 <env> <tag> [migration-alvo]}"
TAG="${2:?}"
TARGET="${3:-}"
RG="$(resource_group "$ENVIRONMENT")"
JOB="$(migrate_job "$ENVIRONMENT")"
IMAGE="$(registry_name "$ENVIRONMENT").azurecr.io/advocacia-migrator:$TAG"

log "atualizando $JOB para $IMAGE"
update_args=(--name "$JOB" --resource-group "$RG" --image "$IMAGE" --output none)
# Rollback: o bundle aceita o nome da migration alvo como argumento.
[[ -n "$TARGET" ]] && update_args+=(--args "$TARGET")
az containerapp job update "${update_args[@]}"

EXECUTION="$(az containerapp job start --name "$JOB" --resource-group "$RG" --query name -o tsv)"
log "execução $EXECUTION iniciada"

for _ in $(seq 1 120); do
  status="$(az containerapp job execution show --name "$JOB" --resource-group "$RG" \
    --job-execution-name "$EXECUTION" --query properties.status -o tsv)"
  case "$status" in
    Succeeded) log "migrations aplicadas"; break ;;
    Failed|Stopped|Degraded)
      log "migração falhou ($status). Logs: az containerapp job logs show -n $JOB -g $RG --execution $EXECUTION --container migrator"
      exit 1 ;;
  esac
  sleep 10
done
[[ "$status" == "Succeeded" ]] || { log "timeout da migração"; exit 1; }

# Volta os argumentos ao padrão (aplicar tudo) para a próxima execução.
if [[ -n "$TARGET" ]]; then
  az containerapp job update --name "$JOB" --resource-group "$RG" --args "" --output none || true
fi
