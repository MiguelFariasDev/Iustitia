# Runbook — migrations do banco (aplicar e reverter)

O banco tem **duas famílias** de migration, e a ordem entre elas importa:

| Família | Onde | Faz o quê | Como aplica |
|---|---|---|---|
| **EF Core** | `src/**/Persistence/Migrations` | Cria/altera **tabelas, colunas e índices** de cada módulo | Job `caj-advocacia-db-migrate-<env>` (migration bundle) |
| **SQL do Supabase** | `infra/supabase/migrations` | **RLS, grants, constraints e índices especiais** que o EF não modela | `npx supabase db push` |

**Ordem de aplicação: EF primeiro, SQL depois.** As migrations SQL a partir da 010 criam policies e índices sobre tabelas
que o EF cria; invertida, falham com `relation does not exist`. Para **reverter**, a ordem é a inversa: SQL (`.down.sql`)
primeiro, EF depois.

## Antes de qualquer migration em produção

1. **Backup fresco**: dispare o workflow `Backup do Banco` (tier `daily`) e confirme que terminou verde. Não migre sem
   um backup que você consiga restaurar (ver `restauracao-de-backup.md`).
2. **Staging primeiro**, sempre, com um banco parecido com o de produção (volume e extensões).
3. Migration **compatível com a versão anterior da aplicação** durante a troca (expand → migrate → contract): adicionar
   coluna nula/com default é seguro; renomear ou remover coluna não é — faça em duas versões.
4. Toda migration SQL nova **precisa de `.down.sql`** em `infra/supabase/rollback` (o teste `RollbackScriptsTests` falha
   o build se faltar e executa cada script contra o banco completo).

## Aplicar

```bash
# 1) EF Core — job de migração (conecta como DONO do schema, nunca como app_user)
az containerapp job start --name caj-advocacia-db-migrate-<env> --resource-group <rg>
az containerapp job execution list --name caj-advocacia-db-migrate-<env> --resource-group <rg> -o table   # esperar "Succeeded"

# 2) SQL do Supabase
npx supabase db push --workdir infra --db-url "$SUPABASE_DB_URL"

# 3) só então trocar a imagem da API e do Worker
```

O pipeline deve disparar o job **antes** de trocar a imagem da API/Worker (é o que o Terraform do job assume). O job tem
`replica_retry_limit = 0`: se falhar, **não** reexecute sem ler o log — uma migration que falhou no meio pode ter aplicado
parte das mudanças.

## Verificar

```sql
-- EF: última migration aplicada por contexto
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 5;
-- RLS ligado nas tabelas de negócio (nenhuma linha = bom)
SELECT relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND relkind = 'r' AND NOT relrowsecurity
   AND relname IN ('lancamentos','calendar_events','work_tasks','tags','publications','clientes','processos');
```

Depois: `/health/ready` verde, e um fluxo de leitura e um de escrita com um usuário de teste de um escritório.

## Reverter

> Reverter **perde dados** se a migration criou coluna/tabela já preenchida. Se o problema é só uma mudança de código,
> prefira reverter o **deploy** (imagem anterior) e deixar o schema como está — migrations aditivas são compatíveis.

```bash
# 1) SQL — como dono do schema, da migration mais nova para a mais antiga
psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -f infra/supabase/rollback/019_financeiro.down.sql
npx supabase migration repair --status reverted 019 --workdir infra

# 2) EF — o bundle aceita a migration ALVO (a que deve ficar como a última aplicada)
docker run --rm -e ADVOCACIA_DB_CONNECTION="$MIGRATIONS_CONNECTION" advocacia-migrator <NomeDaMigrationAnterior>
```

Casos especiais:

- **`002_rls` (down)**: desliga o isolamento entre escritórios. Exige confirmação na sessão; só em incidente, e com o
  acesso à aplicação **fechado** enquanto durar.
- **`004_custom_access_token_hook` (down)**: desative o hook no painel do Supabase **antes**, senão o login quebra.
- **`015_account_type_claim` não tem down**: ela substitui a função do hook de token. Reverter é reaplicar a versão
  anterior do hook (o conteúdo de `004`) e a claim `account_type` deixa de existir no JWT.
- **`014_audit_immutability` (down)**: devolve a possibilidade de editar a trilha de auditoria. Só em staging.

## Se a migration falhou no meio

1. **Pare**: não repita nem force. Preserve o log do job.
2. Descubra o que foi aplicado (`__EFMigrationsHistory` e o estado das tabelas). O EF aplica cada migration numa transação,
   mas a SQL do Supabase roda como o arquivo vier: confira se ficou **meia** aplicada.
3. Corrija a causa em staging, gere uma **nova** migration (nunca edite uma já aplicada em produção) e reaplique.
4. Em último caso, restaure o backup feito no passo "antes de qualquer migration".
