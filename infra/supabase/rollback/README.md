# Rollback das migrations SQL do Supabase

Scripts "down" de cada arquivo em `../migrations`. Ficam **fora** da pasta
`migrations/` de propósito: a Supabase CLI aplicaria qualquer `.sql` que
estivesse lá.

Procedimento completo (sempre em staging primeiro): `docs/runbooks/database-migration.md`.

Resumo:

```bash
# 1. Aplicar o down (como dono do schema)
psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -f infra/supabase/rollback/004_custom_access_token_hook.down.sql

# 2. Marcar a migration como revertida no histórico da CLI
npx supabase migration repair --status reverted 004 --workdir infra
```

| Migration | Down | Observação |
|---|---|---|
| 000_extensions | não reverte | Extensões são compartilhadas (Supabase usa pgcrypto/uuid-ossp internamente). |
| 001_fulltext | `001_fulltext.down.sql` | Remove índices e a coluna gerada `search_vector`. |
| 002_rls | `002_rls.down.sql` | **Desliga o isolamento entre tenants.** Exige confirmação explícita na sessão. |
| 003_autovacuum | `003_autovacuum.down.sql` | Volta aos parâmetros padrão. |
| 004_custom_access_token_hook | `004_custom_access_token_hook.down.sql` | Desative o hook no Dashboard **antes**, senão o login quebra. |

Toda migration nova deve vir acompanhada do seu `.down.sql` aqui.
