# Performance do Banco de Dados

## Monitoramento com `pg_stat_statements`

Habilitada via `infra/supabase/migrations/000_extensions.sql`. Consultas
úteis:

```sql
-- Top 10 queries por tempo total acumulado
SELECT query, calls, total_exec_time, mean_exec_time
FROM pg_stat_statements
ORDER BY total_exec_time DESC
LIMIT 10;

-- Top 10 queries por tempo médio (identifica queries pontualmente lentas,
-- não só as mais chamadas)
SELECT query, calls, mean_exec_time
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;
```

Resetar as estatísticas (útil antes de um teste de carga):
```sql
SELECT pg_stat_statements_reset();
```

## `EXPLAIN ANALYZE`

Antes de declarar um índice novo (ou remover um existente), confirmar com
dados reais, não intuição:

```sql
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT * FROM users WHERE tenant_id = '...' AND role = 'Lawyer';
```

Sinais de alerta no plano: `Seq Scan` em tabela grande onde se esperava
`Index Scan`; `Rows Removed by Filter` muito maior que `Rows`; ordenação
(`Sort`) que poderia ser evitada com um índice já ordenado.

## Tuning de autovacuum

Tabelas de alto volume de escrita (`audit_logs`, `outbox_messages`) têm
`autovacuum_vacuum_scale_factor`/`autovacuum_analyze_scale_factor`
reduzidos (ver `infra/supabase/migrations/003_autovacuum.sql`) para que o
autovacuum rode com mais frequência e as estatísticas do planner fiquem
atualizadas — o padrão do Postgres (scale factor 0.2/0.1) é pensado para
tabelas de uso geral, não para tabelas append-only de alto volume.

Verificar se o autovacuum está acompanhando o volume de escrita:
```sql
SELECT relname, n_dead_tup, n_live_tup, last_autovacuum, last_autoanalyze
FROM pg_stat_user_tables
ORDER BY n_dead_tup DESC;
```

## Connection Pooling (PgBouncer)

O Supabase já expõe um endpoint com PgBouncer embutido (modo transaction)
para conexões de aplicações serverless/com muitas conexões curtas. **TODO
(antes de produção):** decidir se o backend .NET (que mantém pool de
conexões próprio via Npgsql) conecta direto ao Postgres ou via PgBouncer —
Npgsql já faz pooling de conexão no processo, então PgBouncer só agrega
valor se o número de instâncias do backend (Container Apps escalando
horizontalmente) multiplicado pelo pool size de cada instância se
aproximar do limite de conexões do Postgres.

## Checklist antes de otimizar

1. O problema apareceu em `pg_stat_statements` com dados reais, ou é uma
   suposição? Não otimizar sem medir.
2. `EXPLAIN ANALYZE` confirma que falta um índice, ou o índice existe mas
   não está sendo usado (estatísticas desatualizadas — rodar `ANALYZE`)?
3. O índice novo vale o custo de escrita adicional (todo índice torna
   INSERT/UPDATE mais lento)? Ver `docs/database/indexes.md` para os
   índices já existentes antes de duplicar cobertura.
