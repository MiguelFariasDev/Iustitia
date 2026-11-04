#!/usr/bin/env bash
# Mantém só as N tags mais recentes de um repositório do ACR — MAS nunca apaga
# uma tag ainda usada por alguma revisão (ativa ou não) dos Container Apps/Jobs do
# ambiente: é dessas revisões que o rollback depende.
#
#   ./scripts/acr-retention.sh <acr-name> <resource-group> <repository> [keep=10]
set -euo pipefail

REGISTRY="${1:?uso: $0 <acr> <resource-group> <repositório> [manter]}"
RESOURCE_GROUP="${2:?}"
REPOSITORY="${3:?}"
KEEP="${4:-10}"

in_use="$(
  {
    for app in $(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[].name" -o tsv); do
      az containerapp revision list --name "$app" --resource-group "$RESOURCE_GROUP" --all \
        --query "[].properties.template.containers[].image" -o tsv
    done
    az containerapp job list --resource-group "$RESOURCE_GROUP" \
      --query "[].properties.template.containers[].image" -o tsv
  } | awk -F: -v repo="/${REPOSITORY}" 'index($1, repo) { print $NF }' | sort -u
)"

mapfile -t tags < <(az acr repository show-tags --name "$REGISTRY" --repository "$REPOSITORY" \
  --orderby time_desc -o tsv 2>/dev/null || true)

deleted=0
for index in "${!tags[@]}"; do
  tag="${tags[$index]}"
  (( index < KEEP )) && continue
  if grep -qxF "$tag" <<<"$in_use"; then
    echo "mantendo $REPOSITORY:$tag (em uso por uma revisão)"
    continue
  fi
  az acr repository delete --name "$REGISTRY" --image "$REPOSITORY:$tag" --yes --output none
  echo "removido $REPOSITORY:$tag"
  deleted=$((deleted + 1))
done

echo "$REPOSITORY: ${#tags[@]} tags, $deleted removidas, mantidas as $KEEP mais recentes + as em uso."
