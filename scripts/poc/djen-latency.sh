#!/usr/bin/env bash
# ============================================================================
# djen-latency.sh — Caracteriza a API pública de comunicações do DJEN/CNJ:
# latência (P50/P90/P95/P99), taxa de erro e comportamento sob rajada.
#
# Mede o DJEN DIRETAMENTE (curl), sem passar pelo DjenClient: o objetivo é
# caracterizar a dependência externa, não o nosso código. A medição ponta a
# ponta do job é feita separadamente (ver djen-capture-run.sh).
#
# Uso:  scripts/poc/djen-latency.sh [n_requisicoes] [concorrencia]
# ============================================================================
set -uo pipefail

BASE="${CNJ_BASE_URL:-https://comunicaapi.pje.jus.br/api/v1/comunicacao}"
N="${1:-30}"
CONCURRENCY="${2:-1}"
OUT="${OUT_DIR:-/tmp}/djen-latency-$(date +%Y%m%d-%H%M%S).csv"

TRIBUNAIS=(TJSP TJRJ TJMG TJCE)
HOJE=$(date -u +%Y-%m-%d)
ONTEM=$(date -u -d '1 day ago' +%Y-%m-%d)

echo "seq,tribunal,http_code,tempo_total_s,tamanho_bytes,timestamp" > "$OUT"

requisicao() {
  local seq=$1
  local tribunal=${TRIBUNAIS[$((seq % ${#TRIBUNAIS[@]}))]}
  local url="${BASE}?dataDisponibilizacaoInicio=${ONTEM}&dataDisponibilizacaoFim=${HOJE}&siglaTribunal=${tribunal}&pagina=1&itensPorPagina=100"

  # -m 60: teto generoso de propósito — queremos MEDIR o timeout real do CNJ,
  # não impor o nosso.
  local resultado
  resultado=$(curl -s -m 60 -o /dev/null -w '%{http_code},%{time_total},%{size_download}' "$url" 2>/dev/null) \
    || resultado="000,60.0,0"

  echo "${seq},${tribunal},${resultado},$(date -u +%H:%M:%S)" >> "$OUT"
}

echo "Medindo $N requisições ao DJEN (concorrência $CONCURRENCY)..."
for ((i = 0; i < N; i++)); do
  if [[ "$CONCURRENCY" -gt 1 ]]; then
    requisicao "$i" &
    (( (i + 1) % CONCURRENCY == 0 )) && wait
  else
    requisicao "$i"
  fi
done
wait

echo "$OUT"
