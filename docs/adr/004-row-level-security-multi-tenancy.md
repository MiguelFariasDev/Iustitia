# ADR-004: Row-Level Security para multi-tenancy

**Status:** Aceito

## Contexto

O isolamento de dados entre escritórios (tenants) é um **princípio
inegociável** do produto (ver `claude.md`, seção 3, e
`docs/requisitos/01-visao-geral-e-principios.md`): um bug que vaze dados de
um escritório para outro é inaceitável — tanto por confidencialidade quanto
por sigilo profissional (Código de Ética da OAB). Depender apenas de
filtros `WHERE tenant_id = @tenantId` na camada de aplicação é frágil: um
único `Include`, join manual ou query Dapper esquecida quebra o isolamento
silenciosamente.

## Decisão

Aplicar **Row-Level Security (RLS)** nativa do PostgreSQL em **todas** as
tabelas de negócio com `tenant_id` (ver `docs/requisitos/09-modelo-de-dados.md`),
como camada de defesa **em profundidade**, complementar (não substituta) ao
filtro global do EF Core:

```sql
CREATE POLICY tenant_isolation ON processes
  USING (tenant_id = current_setting('app.current_tenant')::uuid);
```

A aplicação também aplica um filtro global equivalente no `BaseDbContext`
(ver `BuildingBlocks.Infrastructure`, Etapa 2) a partir de `ITenantContext`.
Mesmo que o filtro da aplicação falhe por erro de programação, o RLS no
banco impede o vazamento.

## Consequências

**Positivas:**
- Isolamento garantido mesmo diante de bugs na camada de aplicação
  (defesa em profundidade).
- Auditável diretamente no banco, independente do código da aplicação.

**Negativas / trade-offs:**
- Exige que a conexão do EF Core configure `app.current_tenant` por sessão/
  transação (implementado na Etapa 0.2, via `TenantContextInterceptor`).
- Policies precisam ser criadas e testadas para cada nova tabela — processo
  que deve fazer parte do checklist de toda migration.
- Testes de integração devem validar isolamento com dois tenants distintos
  (ver critério de aceite da Fase 0 em `claude.md`).

## Atualização — Etapa 0.2 (implementação completa)

RLS foi implementado e **validado empiricamente contra Postgres real**
(`TenantIsolationTests`, com Testcontainers) — não apenas configurado, mas
testado nos três cenários que importam:

1. **`app_user` (role de aplicação, sem privilégio de dono de tabela) com
   tenant definido** → só enxerga linhas do próprio tenant.
2. **`app_user` sem tenant definido** → **zero linhas** (fail closed — a
   ausência de `app.current_tenant` nunca degrada para "ver tudo").
3. **`postgres` (dono das tabelas)** → bypassa RLS por padrão do Postgres
   (dono de tabela e superusers não são afetados por RLS a menos que
   `FORCE ROW LEVEL SECURITY` seja usado) — por isso a aplicação **nunca**
   deve conectar como dono das tabelas em produção.

**Policies criadas** (`infra/supabase/migrations/002_rls.sql`):
`tenant_isolation_tenants`, `tenant_isolation_users`,
`tenant_isolation_audit_logs`, `tenant_isolation_settings`,
`tenant_isolation_feature_flags`. `outbox_messages` não recebe RLS — é uma
tabela técnica global, sem `tenant_id`.

**Mecanismo técnico:**
```sql
USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
```
O segundo argumento `true` (`missing_ok`) faz `current_setting` retornar
`NULL` em vez de lançar erro quando a variável de sessão não foi definida
— e `tenant_id = NULL` nunca é verdadeiro em SQL, garantindo o
comportamento fail-closed do item 2 acima.

**Role de aplicação:** `app_user`, criada com `GRANT SELECT, INSERT,
UPDATE, DELETE` (sem `CREATEDB`/`CREATEROLE`/`SUPERUSER`). A senha é
definida fora do controle de versão (Key Vault em produção).

**Armadilha de implementação encontrada e corrigida:** o filtro global de
tenant no C# (`BaseDbContext`) só funciona corretamente se a expressão do
`HasQueryFilter` referenciar `this.<propriedade>` (uma propriedade da
própria instância do DbContext), nunca uma variável local capturada — o EF
Core cacheia o modelo compilado por **tipo** de DbContext, não por
instância, e só rebinda `this` corretamente para a instância que está de
fato executando a query. Isso é uma camada de defesa em **complemento** ao
RLS, que continua sendo a garantia definitiva.

## Atualização — Etapa 0.3 (JWT como origem do tenant + SET vs SET LOCAL)

Com a autenticação via Supabase (ADR-003), `ITenantContext` passa a ser
alimentado a partir da claim `tenant_id` do JWT (injetada pelo Custom
Access Token Hook, ver ADR-029), lida por `CurrentUserAccessor` e aplicada
por `TenantContextMiddleware` (`Platform.Api`) no início de cada
requisição autenticada — antes de qualquer acesso a dados.

**Alternativa considerada e descartada:** policies RLS lendo o tenant
direto de `auth.jwt() ->> 'tenant_id'`, sem depender de `SET
app.current_tenant` nenhum. Foi descartada porque acopla toda policy RLS a
estar rodando numa conexão autenticada pelo Supabase (`auth.jwt()` só
existe nesse contexto) — testes de integração, jobs em background,
migrations e qualquer código que converse com o Postgres fora do caminho
HTTP autenticado (ver `TenantIsolationTests`, que conecta como `app_user`
diretamente) deixariam de funcionar. Manter `current_setting('app.current_
tenant', true)` (já em produção desde a Etapa 0.2) preserva essa
flexibilidade — o JWT só passou a ser **uma fonte a mais** de onde
`ITenantContext` pode ser populado, não uma mudança na policy em si.

**`SET` (sessão) em vez de `SET LOCAL` (transação) no
`TenantContextInterceptor`:** `SET LOCAL` exige uma transação explícita já
aberta — expira automaticamente no `COMMIT`/`ROLLBACK`, o que é mais
estrito, mas um `SELECT` avulso do EF Core (`AsNoTracking`, leituras fora
de `SaveChangesAsync`) frequentemente **não abre transação nenhuma**, e
`SET LOCAL` fora de transação é silenciosamente equivalente a `SET` até o
fim da sessão mesmo assim — só que de forma menos previsível. Usar `SET`
diretamente, reaplicado a **cada** `ConnectionOpened`/`ConnectionOpenedAsync`
(não só a primeira vez, pois o Npgsql reutiliza conexões físicas do pool
entre requisições de tenants diferentes), garante que a variável de sessão
está sempre correta para a conexão em uso, independente de haver ou não
uma transação ativa no momento da query. O trade-off aceito: se algum
código reutilizasse a mesma conexão fora do ciclo de vida do
`DbContext`/interceptor (não é o caso hoje), o valor de `app.current_
tenant` poderia vazar entre usos — mitigado por nunca compartilhar
conexões fora do pool gerenciado pelo Npgsql/EF Core.

**Bug encontrado e corrigido nos testes de integração HTTP desta etapa:**
o filtro global de tenant do EF Core (`BaseDbContext`) usava
`!TenantContext.HasTenant || EF.Property<Guid>(...) ==
TenantContext.TenantId` — mas o EF Core traduz essa expressão para SQL
parametrizado, e o parâmetro do lado direito do `||` é sempre avaliado
para virar o valor do parâmetro, mesmo quando o lado esquerdo já tornaria
o resultado verdadeiro (não existe curto-circuito real em runtime, só na
árvore de expressão). `ITenantContext.TenantId` lança
`InvalidOperationException` quando nenhum tenant foi definido (ver
`AsyncLocalTenantContext`) — então **qualquer** query a uma entidade
`IHasTenant` feita durante uma requisição anônima (Login, cadastro de
escritório) quebrava com 500, mesmo o filtro nunca precisando de fato
restringir nada. Só apareceu quando os testes passaram a bater nos
endpoints HTTP de verdade (`AuthFlowTests`) — os testes de repositório da
Etapa 0.2 sempre definiam um tenant antes de consultar. Corrigido com um
getter auxiliar (`TenantIdForFilters`) que devolve `Guid.Empty` em vez de
lançar quando `HasTenant` é falso — o valor é irrelevante nesse caso
(o `||` já garante `true`), só não pode lançar.
