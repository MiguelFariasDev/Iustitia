# Estratégia de Índices

> Ver ADR-026 para o racional completo. Este documento é a referência
> operacional: quais índices existem, por quê, e onde declarar um novo.

## Onde declarar cada tipo de índice

| Tipo | Onde declarar | Exemplo |
|---|---|---|
| Primary key | Fluent API (`HasKey`) | `pk_tenants` |
| Unique | Fluent API (`HasIndex().IsUnique()`) | `ux_tenants_cnpj` |
| Composto | Fluent API (`HasIndex(x => new { ... })`) | `ix_users_tenant_role` |
| Parcial | Fluent API (`HasIndex().HasFilter(...)`) | `ix_users_tenant_active_only` |
| Full-text (GIN + tsvector) | SQL raw, migration do Supabase | `ix_users_search` |
| Trigram / busca fuzzy (GIN) | SQL raw, migration do Supabase | `ix_users_name_trgm` |
| BRIN (séries temporais monotônicas) | SQL raw, migration do Supabase | `ix_audit_logs_created_brin` |
| Covering (`INCLUDE`) | SQL raw (`migrationBuilder.Sql`) na migration do EF Core | — (nenhum ainda; entra na Fase 2 com `processes`) |

**Regra prática:** se o Fluent API do EF Core expressa o índice
diretamente, use Fluent API (fica versionado junto com o modelo C#,
gera migration automaticamente). Se depende de um recurso específico do
Postgres que o EF Core não conhece (GIN, BRIN, coluna gerada,
`gin_trgm_ops`), use SQL raw numa migration do Supabase.

## Índices existentes (Platform — Etapa 0.2)

### `tenants`
- `pk_tenants` — PK em `id`
- `ux_tenants_cnpj` — unique em `cnpj`
- `ix_tenants_status` — em `status`

### `users`
- `pk_users` — PK em `id`
- `ux_users_tenant_email` — unique composto em `(tenant_id, email)`
- `ix_users_tenant_role` — composto em `(tenant_id, role)`
- `ix_users_tenant_active` — composto em `(tenant_id, is_active)`
- `ix_users_tenant_specialty` — composto em `(tenant_id, specialty)` — usado
  pelo Kanban de Equipe (agrupamento por especialidade)
- `ix_users_tenant_active_only` — parcial em `tenant_id`, `WHERE is_deleted = false`
- `ix_users_search` — **GIN** em `search_vector` (tsvector, português) — busca global
- `ix_users_name_trgm` — **GIN + pg_trgm** em `name` — busca fuzzy

### `audit_logs`
- `pk_audit_logs` — PK em `id`
- `ix_audit_logs_tenant_created` — composto em `(tenant_id, created_at desc)`
- `ix_audit_logs_entity` — composto em `(entity_type, entity_id)`
- `ix_audit_logs_user_created` — composto em `(user_id, created_at desc)`
- `ix_audit_logs_created_brin` — **BRIN** em `created_at` — tabela só cresce,
  nunca é atualizada; BRIN é muito mais compacto que B-tree aqui

### `settings`
- `pk_settings` — PK em `id`
- `ux_settings_tenant_key` — unique composto em `(tenant_id, key)`

### `feature_flags`
- `pk_feature_flags` — PK em `id`
- `ux_feature_flags_tenant_key` — unique composto em `(tenant_id, key)`

### `outbox_messages`
- `pk_outbox_messages` — PK em `id`
- `ix_outbox_pending` — parcial em `created_at`, `WHERE status = 'Pending'`
  — o worker só lê pendentes, então o índice só precisa cobrir esse subconjunto
- `ix_outbox_status_created` — composto em `(status, created_at)`

## Ordem das colunas em índices compostos

Regra geral seguida acima: **coluna de maior seletividade/filtro mais
comum primeiro**, especialmente `tenant_id` — quase toda query do sistema
já filtra por tenant (via RLS + filtro global), então colocá-lo primeiro
em índices compostos permite que o planner use o índice mesmo em queries
que só filtram por tenant (sem a segunda coluna).

## Monitoramento

- `pg_stat_statements` (habilitado via
  `infra/supabase/migrations/000_extensions.sql`) — identifica queries
  lentas/frequentes de verdade, em vez de otimizar por intuição.
- `EXPLAIN ANALYZE` antes de declarar um índice novo em produção — ver
  `docs/database/performance.md`.
- Consultar índices existentes: `SELECT indexname, tablename FROM
  pg_indexes WHERE schemaname = 'public';`
- Verificar uso real de um índice (detectar índices "mortos"):
  `SELECT * FROM pg_stat_user_indexes WHERE indexrelname = '<nome>';`

## Testes

Todos os índices listados acima são verificados por teste de integração
real contra Postgres — ver `tests/IntegrationTests/Platform.IntegrationTests/IndexTests.cs`.
Ao adicionar um índice novo, adicionar o caso correspondente lá.
