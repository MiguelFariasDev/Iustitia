#!/usr/bin/env bash
# Prepara uma assinatura Azure para o Terraform e o GitHub Actions (rodar UMA vez
# por ambiente, por alguém com Owner na assinatura). Idempotente.
#
#   ./scripts/azure-bootstrap.sh <dev|staging|prod> <owner/repo>
#   ex.: ./scripts/azure-bootstrap.sh staging MiguelFariasDev/Iustitia
#
# Cria:
#   - rg-advocacia-shared + storage account do Terraform state (versionamento,
#     soft delete de blobs e containers, sem shared key)
#   - rg-advocacia-<env> (o Terraform só lê; ver ADR-049)
#   - identidade id-advocacia-github-<env> com credenciais federadas (OIDC) para o
#     GitHub Actions — nenhuma senha/client secret existe
#   - permissões da identidade, com escopo mínimo (RG do ambiente + state)
#
# Variáveis opcionais: LOCATION (brazilsouth), TFSTATE_STORAGE_ACCOUNT
# (stadvocaciatfstate — troque se o nome já estiver em uso no Azure).
set -euo pipefail

ENVIRONMENT="${1:?uso: $0 <dev|staging|prod> <owner/repo>}"
REPOSITORY="${2:?uso: $0 <dev|staging|prod> <owner/repo>}"
LOCATION="${LOCATION:-brazilsouth}"
SHARED_RG="rg-advocacia-shared"
STATE_ACCOUNT="${TFSTATE_STORAGE_ACCOUNT:-stadvocaciatfstate}"
ENV_RG="rg-advocacia-${ENVIRONMENT}"
IDENTITY="id-advocacia-github-${ENVIRONMENT}"

case "$ENVIRONMENT" in
  dev) GITHUB_ENVIRONMENT="dev" ;;
  staging) GITHUB_ENVIRONMENT="staging" ;;
  prod) GITHUB_ENVIRONMENT="production" ;;
  *) echo "Ambiente inválido: $ENVIRONMENT" >&2; exit 1 ;;
esac

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
echo "Assinatura: $(az account show --query name -o tsv) ($SUBSCRIPTION_ID)"
read -r -p "Continuar o bootstrap de '$ENVIRONMENT' nesta assinatura? [s/N] " answer
[[ "$answer" =~ ^[sS]$ ]] || exit 1

echo "==> Registrando resource providers"
for provider in Microsoft.App Microsoft.ContainerRegistry Microsoft.KeyVault Microsoft.ServiceBus \
  Microsoft.Cache Microsoft.Storage Microsoft.Web Microsoft.Cdn Microsoft.Network \
  Microsoft.OperationalInsights Microsoft.Insights Microsoft.AlertsManagement \
  Microsoft.ManagedIdentity Microsoft.Consumption; do
  az provider register --namespace "$provider" --output none
done

echo "==> Resource groups"
az group create --name "$SHARED_RG" --location "$LOCATION" \
  --tags Project=Advocacia ManagedBy=Bootstrap --output none
az group create --name "$ENV_RG" --location "$LOCATION" \
  --tags Project=Advocacia Environment="$ENVIRONMENT" ManagedBy=Terraform --output none

echo "==> Storage account do Terraform state ($STATE_ACCOUNT)"
if ! az storage account show --name "$STATE_ACCOUNT" --resource-group "$SHARED_RG" --output none 2>/dev/null; then
  az storage account create --name "$STATE_ACCOUNT" --resource-group "$SHARED_RG" --location "$LOCATION" \
    --sku Standard_GRS --kind StorageV2 --min-tls-version TLS1_2 --https-only true \
    --allow-blob-public-access false --allow-shared-key-access false --output none
fi
az storage account blob-service-properties update --account-name "$STATE_ACCOUNT" --resource-group "$SHARED_RG" \
  --enable-versioning true \
  --enable-delete-retention true --delete-retention-days 30 \
  --enable-container-delete-retention true --container-delete-retention-days 30 --output none
STATE_ACCOUNT_ID="$(az storage account show --name "$STATE_ACCOUNT" --resource-group "$SHARED_RG" --query id -o tsv)"

# Quem roda o bootstrap também precisa ler/gravar o state (terraform local).
SIGNED_IN_ID="$(az ad signed-in-user show --query id -o tsv)"
az role assignment create --assignee-object-id "$SIGNED_IN_ID" --assignee-principal-type User \
  --role "Storage Blob Data Contributor" --scope "$STATE_ACCOUNT_ID" --output none 2>/dev/null || true
az storage container create --name tfstate --account-name "$STATE_ACCOUNT" --auth-mode login --output none

echo "==> Identidade do GitHub Actions ($IDENTITY)"
az identity create --name "$IDENTITY" --resource-group "$SHARED_RG" --location "$LOCATION" --output none
CLIENT_ID="$(az identity show --name "$IDENTITY" --resource-group "$SHARED_RG" --query clientId -o tsv)"
PRINCIPAL_ID="$(az identity show --name "$IDENTITY" --resource-group "$SHARED_RG" --query principalId -o tsv)"

create_federated_credential() {
  local name="$1" subject="$2"
  if ! az identity federated-credential show --name "$name" --identity-name "$IDENTITY" \
      --resource-group "$SHARED_RG" --output none 2>/dev/null; then
    az identity federated-credential create --name "$name" --identity-name "$IDENTITY" \
      --resource-group "$SHARED_RG" --issuer "https://token.actions.githubusercontent.com" \
      --subject "$subject" --audiences "api://AzureADTokenExchange" --output none
  fi
}

# Jobs com `environment:` recebem o subject environment:<nome> (deploys, terraform).
# Só em prod existe um segundo subject — a branch main — para os jobs que precisam
# rodar SEM aprovação manual: backups agendados e o `terraform plan` pós-merge
# (ver docs/devops/ci-cd.md, "Identidades").
create_federated_credential "github-environment" "repo:${REPOSITORY}:environment:${GITHUB_ENVIRONMENT}"
if [[ "$ENVIRONMENT" == "prod" ]]; then
  create_federated_credential "github-main" "repo:${REPOSITORY}:ref:refs/heads/main"
fi

echo "==> Permissões da identidade (aguardando propagação no Entra ID)"
sleep 30
ENV_RG_ID="$(az group show --name "$ENV_RG" --query id -o tsv)"

az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Contributor" --scope "$ENV_RG_ID" --output none
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" --scope "$STATE_ACCOUNT_ID" --output none

# O Terraform cria role assignments (AcrPull, Key Vault Secrets User, ...). Em vez de
# User Access Administrator, a identidade recebe RBAC Administrator com CONDIÇÃO:
# só pode atribuir/remover os papéis abaixo — nunca Owner/Contributor/User Access
# Administrator. Um workflow comprometido não consegue escalar privilégio.
role_id() { az role definition list --name "$1" --query "[0].name" -o tsv; }
ALLOWED_ROLES="$(role_id AcrPull), $(role_id AcrPush), $(role_id 'Key Vault Secrets User'), $(role_id 'Key Vault Secrets Officer'), $(role_id 'Storage Blob Data Contributor')"
CONDITION="((!(ActionMatches{'Microsoft.Authorization/roleAssignments/write'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${ALLOWED_ROLES}})) AND ((!(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${ALLOWED_ROLES}}))"
az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
  --role "Role Based Access Control Administrator" --scope "$ENV_RG_ID" \
  --condition "$CONDITION" --condition-version "2.0" --output none

# Produção nunca recompila: promove a imagem já testada em staging via
# `az acr import`, o que exige leitura no registry de staging.
if [[ "$ENVIRONMENT" == "prod" ]]; then
  if STAGING_RG_ID="$(az group show --name rg-advocacia-staging --query id -o tsv 2>/dev/null)"; then
    for role in Reader AcrPull; do
      az role assignment create --assignee-object-id "$PRINCIPAL_ID" --assignee-principal-type ServicePrincipal \
        --role "$role" --scope "$STAGING_RG_ID" --output none
    done
  else
    echo "AVISO: rg-advocacia-staging ainda não existe — rode o bootstrap de staging e depois este de novo" \
      "(a identidade de prod precisa ler o ACR de staging para promover imagens)." >&2
  fi
fi

cat <<EOF

Bootstrap de '$ENVIRONMENT' concluído.

Variáveis do environment '$GITHUB_ENVIRONMENT' no GitHub (scripts/github-setup.sh as grava):
  AZURE_CLIENT_ID=$CLIENT_ID
  AZURE_TENANT_ID=$TENANT_ID
  AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID

Para rodar o Terraform localmente:
  export TF_VAR_subscription_id=$SUBSCRIPTION_ID
  export TF_VAR_github_actions_principal_id=$PRINCIPAL_ID
  cd infra/terraform
  terraform init -backend-config=environments/$ENVIRONMENT/backend.hcl
  terraform plan -var-file=environments/$ENVIRONMENT/terraform.tfvars

Guarde também GITHUB_ACTIONS_PRINCIPAL_ID=$PRINCIPAL_ID (variável do environment).
EOF
