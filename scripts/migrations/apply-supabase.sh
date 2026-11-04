#!/usr/bin/env bash
#
# Aplica o schema completo (EF Core + SQL) em um Postgres do Supabase.
#
# Uso:
#   export SUPABASE_DB_URL='postgresql://postgres.<ref>:<senha>@<host>:5432/postgres'
#   ./scripts/migrations/apply-supabase.sh
#
# A URI está em Project Settings > Database > Connection string > URI.
# Nada é escrito em disco: a senha vive só na variável de ambiente desta sessão.
#
# Depois deste script, ative o hook manualmente:
#   Authentication > Hooks > Customize Access Token (JWT) Claims
#   > Postgres > public.custom_access_token_hook > Enable
#
set -euo pipefail

cd "$(dirname "$0")/../.."

if [[ -z "${SUPABASE_DB_URL:-}" ]]; then
  echo "erro: defina SUPABASE_DB_URL (postgresql://...)" >&2
  exit 1
fi

command -v psql >/dev/null || { echo "erro: psql não encontrado" >&2; exit 1; }

# O EF Core usa formato Npgsql (Host=...;Port=...), não URI. Converte sem imprimir a senha.
ADVOCACIA_DB_CONNECTION="$(python3 - <<'PY'
import os, urllib.parse as u
p = u.urlparse(os.environ["SUPABASE_DB_URL"])
print(
    f"Host={p.hostname};Port={p.port or 5432};Database={(p.path or '/postgres').lstrip('/')};"
    f"Username={u.unquote(p.username or '')};Password={u.unquote(p.password or '')};"
    "SSL Mode=Require;Trust Server Certificate=true"
)
PY
)"
export ADVOCACIA_DB_CONNECTION

echo "==> 1/3  Schema via EF Core"
dotnet ef database update \
  --project src/Platform/Platform.Infrastructure \
  --context PlatformDbContext
dotnet ef database update \
  --project src/Modules/Legal/Legal.Infrastructure \
  --context LegalDbContext

echo "==> 2/3  Migrations SQL (extensões, RLS, índices, hook)"
# Ordem importa: 002 cria a RLS de users, 004 depende de users existir.
for f in 000_extensions 001_fulltext 002_rls 003_autovacuum 004_custom_access_token_hook \
         010_publications_indexes 011_publications_rls 012_clientes_processos; do
  echo "    $f"
  psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -q -f "infra/supabase/migrations/${f}.sql"
done

echo "==> 3/3  Verificação"
psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -tA -c "
  SELECT 'função custom_access_token_hook: ' ||
    CASE WHEN to_regprocedure('public.custom_access_token_hook(jsonb)') IS NULL
         THEN 'AUSENTE' ELSE 'ok' END;
  SELECT 'grant para supabase_auth_admin: ' ||
    CASE WHEN has_function_privilege('supabase_auth_admin',
                                     'public.custom_access_token_hook(jsonb)', 'EXECUTE')
         THEN 'ok' ELSE 'AUSENTE' END;
  SELECT 'tabelas de aplicação: ' || count(*)::text
    FROM information_schema.tables
   WHERE table_schema = 'public'
     AND table_name IN ('users','tenants','clientes','processos','publications');
"

cat <<'FIM'

Schema aplicado. Falta o passo manual (não há API nem CLI para ele):

  Supabase > Authentication > Hooks
    > Customize Access Token (JWT) Claims
    > tipo: Postgres
    > schema: public   função: custom_access_token_hook
    > Enable

Depois, apontar a API para este banco:
  cd src/Hosts/Api && dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<npgsql>"
FIM
