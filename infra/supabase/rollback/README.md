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
| 010–014 (publications, clientes, alerts, atendimentos, auditoria, sugestões) | `010_…` a `014_…down.sql` | Cada `.down.sql` reverte o seu `.sql`. `014_audit_immutability` devolve a edição da trilha de auditoria: só em staging. |
| 014_calendar_contacts | `014_calendar_contacts.down.sql` | Desliga o RLS de agenda e contatos. |
| 015_account_type_claim | **não tem down** | Substitui a função do hook de token: reverter exige reaplicar a versão anterior de `004`. Ver o runbook. |
| 015_ai_usage_logs | `015_ai_usage_logs.down.sql` | |
| 016_drafting | `016_drafting.down.sql` | Desliga o RLS das tabelas de peças e do acervo do cliente. |
| 017_normas_juridicas | `017_normas_juridicas.down.sql` | Remove a coluna full-text e os índices; as tabelas são do EF. |
| 018_tarefas_tags | `018_tarefas_tags.down.sql` | |
| 019_financeiro | `019_financeiro.down.sql` | Desliga o RLS dos lançamentos. |
| 021_movimentacoes_notificacoes | `021_movimentacoes_notificacoes.down.sql` | Desliga o RLS e o CHECK de revisão; reverter o EF (`AddMovimentacoesENotificacoesCliente`) depois apaga as tabelas. |

O `.down.sql` cuida só do que o `.sql` fez (RLS, grants, índices, colunas geradas). As **tabelas** são criadas
pelas migrations do EF Core e revertidas por elas (`efbundle <MigrationAnterior>`).

Toda migration nova deve vir acompanhada do seu `.down.sql` aqui.
