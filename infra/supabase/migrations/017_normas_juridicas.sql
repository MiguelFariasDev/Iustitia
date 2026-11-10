-- ============================================================================
-- 017_normas_juridicas.sql — acervo normativo público do módulo Drafting (ADR-073).
--
-- Tabelas (criadas pela migration do EF Core do DraftingDbContext):
--   normas_juridicas · norma_chunks · norma_versoes · ingestao_jobs
--
-- ACERVO PÚBLICO COMPARTILHADO: leis, súmulas e decisões já públicas. Não há dado de cliente aqui, por isso
-- NÃO há tenant nem RLS (como generation_queue e gpu_instance_logs). Documentos de cliente continuam em
-- indexed_documents (016), com RLS.
--
-- Pré-requisitos: 016_drafting.sql e a migration do EF Core (mesmo comando do 016).
--
-- Este script cuida do que o EF não modela: a coluna full-text gerada, o índice GIN e os grants.
-- ============================================================================

-- Busca por palavra-chave (parte "lexical" da busca híbrida). Coluna GERADA: o PostgreSQL a mantém sozinho
-- a cada INSERT/UPDATE de `texto`, então o EF não precisa conhecê-la. Configuração 'portuguese' (stemming).
-- ATENÇÃO: ts_rank_cd NÃO é BM25 (o PostgreSQL não tem BM25 nativo).
ALTER TABLE norma_chunks
    ADD COLUMN IF NOT EXISTS tsv tsvector
    GENERATED ALWAYS AS (to_tsvector('portuguese', coalesce(texto, ''))) STORED;

CREATE INDEX IF NOT EXISTS ix_norma_chunks_tsv ON norma_chunks USING GIN (tsv);

-- Filtros por assunto (n.assuntos && ARRAY[...]).
CREATE INDEX IF NOT EXISTS ix_normas_juridicas_assuntos ON normas_juridicas USING GIN (assuntos);

-- Só o app_user usa o acervo. Leitura/escrita pelo worker (ingestão) e leitura pela API (busca).
GRANT SELECT, INSERT, UPDATE, DELETE ON public.normas_juridicas, public.norma_chunks, public.norma_versoes, public.ingestao_jobs TO app_user;
