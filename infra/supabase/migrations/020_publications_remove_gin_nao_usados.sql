-- ============================================================================
-- 020_publications_remove_gin_nao_usados.sql — Remove da tabela publications os
-- índices GIN e a coluna gerada search_vector criados em 010 que NENHUMA consulta
-- da aplicação usa hoje.
--
-- Medido em 300 mil publicações (docs/database/performance-consultas.md):
--   - inserir 30 mil linhas: 7,07 s com os GIN  ->  1,07 s sem eles;
--   - tamanho total dos índices: 425 MB  ->  243 MB;
--   - o tsvector STORED recalculado a cada INSERT é o maior custo de escrita.
--
-- A busca por texto do repositório consulta cnj_digits / nome_busca (colunas derivadas),
-- nunca raw_content nem search_vector. Quando a busca full-text por inteiro teor for
-- implementada, o 010 (e este down) mostram como recriar — de preferência ao mesmo tempo
-- que o código que a usa, medindo o custo de escrita antes.
--
-- Atenção: GIN trigram (gin_trgm_ops) NÃO acelera LIKE '%x%' para o app_user — LIKE não é
-- "leakproof" e o Postgres não o aplica como condição de índice por baixo de uma política
-- de RLS. Por isso não se cria índice trigram em cnj_digits (testado: o plano ignora o índice).
-- ============================================================================
DROP INDEX IF EXISTS ix_publications_raw_content_trgm;
DROP INDEX IF EXISTS ix_publications_cnj_number_trgm;
DROP INDEX IF EXISTS ix_publications_search;
ALTER TABLE publications DROP COLUMN IF EXISTS search_vector;
