#!/usr/bin/env bash
# Smoke tests pós-deploy (ver docs/devops/deploy.md). Usado pelo pipeline contra a
# revisão candidata (URL do label "green") ANTES de ela receber tráfego, e contra a
# URL pública depois da troca. Também roda à mão:
#
#   ./scripts/smoke-tests.sh https://api-staging.iustitia.com.br
#
# Login + endpoint protegido só rodam se SMOKE_TEST_EMAIL e SMOKE_TEST_PASSWORD
# estiverem definidos (usuário de teste dedicado, num tenant de teste — nunca um
# usuário real). SMOKE_REQUIRE_AUTH=true transforma a ausência deles em falha.
#
# Saída: 0 = tudo ok; 1 = alguma verificação falhou.
set -uo pipefail

BASE_URL="${1:?uso: $0 <url-base-da-api>}"
BASE_URL="${BASE_URL%/}"
TIMEOUT="${SMOKE_TIMEOUT_SECONDS:-10}"
RETRIES="${SMOKE_RETRIES:-5}"
failures=0

command -v jq >/dev/null || { echo "jq é necessário" >&2; exit 1; }

# Uma revisão recém-ativada pode levar alguns segundos para aquecer (Key Vault,
# conexões): cada verificação tenta algumas vezes antes de falhar.
request() {
  local method="$1" url="$2" body="${3:-}" token="${4:-}"
  local attempt output
  for attempt in $(seq 1 "$RETRIES"); do
    local args=(--silent --show-error --max-time "$TIMEOUT" --output /tmp/smoke-body.$$ --write-out '%{http_code}' -X "$method")
    args+=(-H "Accept: application/json" -H "X-Correlation-Id: smoke-$(date +%s)-$attempt")
    [[ -n "$body" ]] && args+=(-H "Content-Type: application/json" --data "$body")
    [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
    output="$(curl "${args[@]}" "$url" 2>/dev/null)" || output="000"
    [[ "$output" =~ ^2 ]] && { echo "$output"; return 0; }
    sleep $((attempt * 2))
  done
  echo "$output"
  return 1
}

check() {
  local name="$1" expected="$2" actual="$3"
  if [[ "$actual" == "$expected" ]]; then
    printf '  ok    %-40s %s\n' "$name" "$actual"
  else
    printf '  FALHA %-40s esperado %s, obtido %s\n' "$name" "$expected" "$actual"
    failures=$((failures + 1))
  fi
}

echo "Smoke tests em $BASE_URL"

check "GET /health/live" 200 "$(request GET "$BASE_URL/health/live")"
check "GET /health/ready" 200 "$(request GET "$BASE_URL/health/ready")"

status="$(request GET "$BASE_URL/api/v1/version")"
check "GET /api/v1/version" 200 "$status"
if [[ "$status" == 200 ]]; then
  echo "        $(jq -c '{version, environment}' /tmp/smoke-body.$$ 2>/dev/null)"
  if [[ -n "${EXPECTED_ENVIRONMENT:-}" ]]; then
    check "ambiente da API" "$EXPECTED_ENVIRONMENT" "$(jq -r '.environment' /tmp/smoke-body.$$)"
  fi
fi

# Endpoint protegido sem token precisa responder 401 (e não 200/500).
check "GET /api/v1/me sem token -> 401" 401 "$(curl --silent --max-time "$TIMEOUT" --output /dev/null --write-out '%{http_code}' "$BASE_URL/api/v1/me")"

if [[ -n "${SMOKE_TEST_EMAIL:-}" && -n "${SMOKE_TEST_PASSWORD:-}" ]]; then
  login_body="$(jq -nc --arg email "$SMOKE_TEST_EMAIL" --arg password "$SMOKE_TEST_PASSWORD" '{email: $email, password: $password}')"
  status="$(request POST "$BASE_URL/api/v1/auth/login" "$login_body")"
  check "POST /api/v1/auth/login" 200 "$status"
  token="$(jq -r '.accessToken // empty' /tmp/smoke-body.$$ 2>/dev/null)"
  if [[ -n "$token" ]]; then
    # Mascara o token no log do GitHub Actions (fora do Actions, nunca é impresso).
    [[ "${GITHUB_ACTIONS:-}" == "true" ]] && echo "::add-mask::$token"
    check "GET /api/v1/me com JWT" 200 "$(request GET "$BASE_URL/api/v1/me" "" "$token")"
    check "tenant_id presente no JWT" "presente" \
      "$([[ -n "$(jq -r '.tenantId // empty' /tmp/smoke-body.$$ 2>/dev/null)" ]] && echo presente || echo ausente)"
  else
    check "accessToken no login" "presente" "ausente"
  fi
elif [[ "${SMOKE_REQUIRE_AUTH:-false}" == "true" ]]; then
  check "credenciais do usuário de smoke test" "definidas" "ausentes"
else
  echo "  --    login/endpoint protegido: pulado (SMOKE_TEST_EMAIL/SMOKE_TEST_PASSWORD não definidos)"
fi

rm -f /tmp/smoke-body.$$

if (( failures > 0 )); then
  echo "Smoke tests: $failures falha(s)."
  exit 1
fi
echo "Smoke tests: tudo ok."
