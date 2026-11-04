#!/usr/bin/env bash
# ============================================================================
# run-capture.sh — Executa o Worker com a captura do CNJ configurada para a
# prova de conceito (Etapa 1.3) e aguarda N execuções do job.
#
# Uso: scripts/poc/run-capture.sh <segundos> [modo]
#      modo = processo (padrão) | tribunal | ambos
# ============================================================================
set -uo pipefail

DURACAO="${1:-90}"
MODO="${2:-processo}"
TENANT="${POC_TENANT_ID:-11111111-1111-4111-8111-111111111111}"
LOG="${OUT_DIR:-/tmp}/poc-capture-${MODO}-$(date +%H%M%S).log"

# Os 7 processos-alvo: reais, públicos, obtidos do DJEN (ver scripts/seed/poc-tenant.sql).
PROCESSOS=(
  "0000777-07.2018.8.06.0100"   # TJCE
  "0234493-37.2024.8.06.0001"   # TJCE
  "0815916-58.2022.8.19.0004"   # TJRJ
  "1697232-12.2015.8.13.0024"   # TJMG
  "5000230-98.2024.8.13.0382"   # TJMG
  "4005347-92.2026.8.26.0176"   # TJSP
  "1506425-76.2025.8.26.0071"   # TJSP
)

env_args=(
  DOTNET_ENVIRONMENT=Development
  Jobs__CnjCapture__Enabled=true
  Jobs__CnjCapture__CronExpression="* * * * *"
  Jobs__CnjCapture__MaxPages=50
  Jobs__CnjCapture__PageSize=100
  Jobs__CnjCapture__TenantIds__0="$TENANT"
  # Desliga os jobs do Platform: só poluem o log da PoC.
  Jobs__HealthCheck__Enabled=false
  Jobs__AuditLogCleanup__Enabled=false
)

# Arrays de configuração no .NET fazem MERGE por índice com o appsettings — para
# realmente zerar a lista de tribunais é preciso sobrescrever cada posição com "".
for i in 0 1 2 3; do env_args+=( "Jobs__CnjCapture__Tribunais__${i}=" ); done

if [[ "$MODO" == "processo" || "$MODO" == "ambos" ]]; then
  for i in "${!PROCESSOS[@]}"; do
    env_args+=( "Jobs__CnjCapture__CnjNumbers__${i}=${PROCESSOS[$i]}" )
  done
fi

if [[ "$MODO" == "tribunal" || "$MODO" == "ambos" ]]; then
  env_args+=( "Jobs__CnjCapture__Tribunais__0=TJCE" "Jobs__CnjCapture__LookbackDays=0" )
fi

echo "modo=$MODO tenant=$TENANT log=$LOG"
cd "$(dirname "$0")/../../src/Hosts/Worker"
env "${env_args[@]}" timeout "$DURACAO" dotnet run --no-build --no-launch-profile > "$LOG" 2>&1
echo "$LOG"
