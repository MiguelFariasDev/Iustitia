#!/usr/bin/env bash
# Grava no Key Vault os segredos que NÃO vêm da infraestrutura (Supabase, Anthropic,
# CNJ, connection strings do banco). O Terraform só cria esses segredos com o
# placeholder "SET-ME" e nunca lê/sobrescreve o valor (ver modules/key-vault).
#
#   ./scripts/keyvault-set-secrets.sh <dev|staging|prod> [NomeDoSegredo ...]
#
# Sem nomes, pergunta por todos. Os valores são lidos do terminal sem eco e
# passados ao az por arquivo temporário (0600, apagado ao final) — nunca aparecem
# em argumentos de processo, histórico do shell ou log.
set -euo pipefail

ENVIRONMENT="${1:?uso: $0 <dev|staging|prod> [segredo ...]}"
shift
VAULT="${KEY_VAULT_NAME:-kv-advocacia-${ENVIRONMENT}}"

declare -A DESCRIPTIONS=(
  ["ConnectionStrings--DefaultConnection"]="Postgres (Supabase) como app_user — role sujeita a RLS. Ex.: Host=...;Port=5432;Database=postgres;Username=app_user;Password=...;SSL Mode=Require"
  ["ConnectionStrings--Migrations"]="Postgres (Supabase) como dono do schema (postgres) — só o job de migração usa"
  ["Supabase--Url"]="https://<ref>.supabase.co"
  ["Supabase--AnonKey"]="Chave anon do projeto Supabase"
  ["Supabase--ServiceRoleKey"]="Chave service_role do projeto Supabase"
  ["Anthropic--ApiKey"]="Chave da API Anthropic (uso só a partir da Fase 3)"
  ["Cnj--ApiKey"]="Chave da API do CNJ (uso a partir da Fase 1)"
)

if [[ $# -gt 0 ]]; then
  SECRETS=("$@")
else
  SECRETS=("${!DESCRIPTIONS[@]}")
fi

TMP_FILE="$(mktemp)"
chmod 600 "$TMP_FILE"
trap 'shred -u "$TMP_FILE" 2>/dev/null || rm -f "$TMP_FILE"' EXIT

for name in "${SECRETS[@]}"; do
  echo
  echo "$name — ${DESCRIPTIONS[$name]:-segredo customizado}"
  read -r -s -p "Valor (Enter vazio = pular): " value
  echo
  if [[ -z "$value" ]]; then
    echo "  pulado"
    continue
  fi
  printf '%s' "$value" > "$TMP_FILE"
  unset value
  az keyvault secret set --vault-name "$VAULT" --name "$name" --file "$TMP_FILE" --encoding utf-8 \
    --content-type "set-outside-terraform" --output none
  : > "$TMP_FILE"
  echo "  gravado"
done

echo
echo "Segredos ainda com placeholder em $VAULT:"
for name in "${!DESCRIPTIONS[@]}"; do
  current="$(az keyvault secret show --vault-name "$VAULT" --name "$name" --query value -o tsv 2>/dev/null || true)"
  [[ "$current" == "SET-ME" ]] && echo "  - $name"
done
echo "(Reinicie as revisões da API/Worker para que leiam os novos valores: scripts/deploy/restart.sh $ENVIRONMENT)"
