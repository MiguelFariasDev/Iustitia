#!/usr/bin/env bash
# ============================================================================
# djen-ratelimit.sh — Sonda o rate limit do DJEN: dispara requisições em
# sequência o mais rápido possível e registra em qual delas o 429 aparece,
# depois mede quanto tempo leva para o orçamento se recuperar.
#
# Roda com concorrência 1 de propósito: o objetivo é descobrir o limite de
# TAXA (requisições por janela), não de conexões simultâneas.
# ============================================================================
set -uo pipefail

BASE="${CNJ_BASE_URL:-https://comunicaapi.pje.jus.br/api/v1/comunicacao}"
N="${1:-120}"
HOJE=$(date -u +%Y-%m-%d)
URL="${BASE}?dataDisponibilizacaoInicio=${HOJE}&dataDisponibilizacaoFim=${HOJE}&siglaTribunal=TJSP&pagina=1&itensPorPagina=5"

echo "Disparando até $N requisições sequenciais sem pausa..."
inicio=$(date +%s.%N)
primeiro_429=""
ok=0
for ((i = 1; i <= N; i++)); do
  code=$(curl -s -m 30 -o /dev/null -w '%{http_code}' "$URL" 2>/dev/null || echo 000)
  if [[ "$code" == "429" ]]; then
    if [[ -z "$primeiro_429" ]]; then
      primeiro_429=$i
      agora=$(date +%s.%N)
      echo "PRIMEIRO 429 na requisição #$i após $(echo "$agora - $inicio" | bc)s ($ok com sucesso)"
      break
    fi
  elif [[ "$code" == "200" ]]; then
    ok=$((ok + 1))
  else
    echo "  #$i -> HTTP $code"
  fi
done

if [[ -z "$primeiro_429" ]]; then
  echo "Nenhum 429 em $N requisições — limite não alcançado neste teste."
  exit 0
fi

echo "Medindo recuperação (poll a cada 5s)..."
espera=0
while (( espera < 180 )); do
  sleep 5
  espera=$((espera + 5))
  code=$(curl -s -m 30 -o /dev/null -w '%{http_code}' "$URL" 2>/dev/null || echo 000)
  if [[ "$code" == "200" ]]; then
    echo "RECUPEROU após ~${espera}s de espera."
    exit 0
  fi
done
echo "Ainda bloqueado após ${espera}s."
