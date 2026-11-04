#!/usr/bin/env bash
# Libera (add) ou remove (remove) o IP público do runner atual no firewall do Key
# Vault e do Storage de backups. Só tem efeito no perfil com rede privada (prod),
# onde esses recursos negam acesso público por padrão. Chamado pelos workflows em
# volta de `terraform apply` e do upload de backups, sempre com remove em `always()`.
#
#   ./scripts/ci-firewall.sh <add|remove> <resource-group> <key-vault> <storage-account>
set -euo pipefail

ACTION="${1:?uso: $0 <add|remove> <rg> <key-vault> <storage>}"
RESOURCE_GROUP="${2:?}"
KEY_VAULT="${3:?}"
STORAGE_ACCOUNT="${4:?}"

default_action="$(az keyvault show --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" \
  --query properties.networkAcls.defaultAction -o tsv 2>/dev/null || echo Allow)"
if [[ "$default_action" != "Deny" ]]; then
  echo "Firewall não restritivo neste ambiente — nada a fazer."
  exit 0
fi

RUNNER_IP="$(curl -fsS https://api.ipify.org)"
echo "::add-mask::$RUNNER_IP"

if [[ "$ACTION" == "add" ]]; then
  az keyvault network-rule add --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" --ip-address "$RUNNER_IP/32" --output none
  az storage account network-rule add --account-name "$STORAGE_ACCOUNT" --resource-group "$RESOURCE_GROUP" --ip-address "$RUNNER_IP" --output none
  # Regras de firewall levam alguns segundos para valer.
  sleep 30
else
  az keyvault network-rule remove --name "$KEY_VAULT" --resource-group "$RESOURCE_GROUP" --ip-address "$RUNNER_IP/32" --output none || true
  az storage account network-rule remove --account-name "$STORAGE_ACCOUNT" --resource-group "$RESOURCE_GROUP" --ip-address "$RUNNER_IP" --output none || true
fi
