# Particionamento (placeholder — Fase 8+)

> Este documento é um placeholder. Particionamento só se justifica quando
> as tabelas relevantes realmente crescerem o suficiente — não implementar
> preventivamente.

## Candidatos a particionamento

- **`publications`** (módulo Legal, criado na Fase 2): publicações
  processuais capturadas do CNJ/DJEN — crescimento indefinido, nunca
  expurgado (ver `docs/requisitos/09-modelo-de-dados.md`). Candidata a
  particionamento por `published_at` (range, mensal ou trimestral) quando
  passar de ~10 milhões de linhas.
- **`audit_logs`**: retenção mínima de 5 anos, só cresce por INSERT.
  Candidata a particionamento por `created_at` (range, mensal), o que
  também facilitaria o expurgo de partições inteiras após o prazo de
  retenção legal, em vez de `DELETE` linha a linha.

## Quando revisitar

- Quando `pg_stat_statements` (ver `docs/database/performance.md`) mostrar
  degradação de performance em queries sobre essas tabelas.
- Quando o tamanho da tabela (`pg_total_relation_size`) ultrapassar a
  faixa de dezenas de GB e o autovacuum começar a não acompanhar o volume
  de escrita mesmo com o tuning de `docs/adr/003_autovacuum.sql` (ver
  `infra/supabase/migrations/003_autovacuum.sql`).

## Abordagem recomendada quando chegar a hora

- `PARTITION BY RANGE` no Postgres nativo (não uma extensão externa),
  particionando por data (`published_at`/`created_at`).
- Migração incremental: criar a tabela particionada nova, copiar dados em
  lote, trocar o nome via `ALTER TABLE ... RENAME` numa janela de
  manutenção — nunca particionar uma tabela em produção "no lugar" sem um
  plano de rollback.
- Reavaliar os índices existentes (`docs/database/indexes.md`) — em
  tabelas particionadas, índices locais por partição costumam performar
  melhor que um índice global.
