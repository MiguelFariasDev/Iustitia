-- Reverte 017_normas_juridicas.sql
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.normas_juridicas, public.norma_chunks, public.norma_versoes, public.ingestao_jobs FROM app_user;

DROP INDEX IF EXISTS ix_normas_juridicas_assuntos;
DROP INDEX IF EXISTS ix_norma_chunks_tsv;
ALTER TABLE norma_chunks DROP COLUMN IF EXISTS tsv;
