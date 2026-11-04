#!/usr/bin/env bash
# Primeiro deploy de um ambiente: publica imagens com a tag "bootstrap" no ACR
# recém-criado, para que o `terraform apply` completo consiga criar os Container
# Apps (ver docs/devops/deploy.md, "Primeiro deploy"). Depois disso, quem publica
# imagens é o pipeline.
#
#   ./scripts/push-bootstrap-images.sh <dev|staging|prod>
set -euo pipefail

ENVIRONMENT="${1:?uso: $0 <dev|staging|prod>}"
REGISTRY="${ACR_NAME:-acradvocacia${ENVIRONMENT}}"
LOGIN_SERVER="$(az acr show --name "$REGISTRY" --query loginServer -o tsv)"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

az acr login --name "$REGISTRY"

build_and_push() {
  local repository="$1" dockerfile="$2"
  docker build --file "$ROOT/$dockerfile" --tag "$LOGIN_SERVER/$repository:bootstrap" "$ROOT"
  docker push "$LOGIN_SERVER/$repository:bootstrap"
}

build_and_push advocacia-api src/Hosts/Api/Dockerfile
build_and_push advocacia-worker src/Hosts/Worker/Dockerfile
build_and_push advocacia-migrator infra/docker/migrator.Dockerfile
