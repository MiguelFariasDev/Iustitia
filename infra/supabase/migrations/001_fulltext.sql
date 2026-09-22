-- ============================================================================
-- 001_fulltext.sql — Busca full-text, trigram e índice BRIN
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisito: 000_extensions.sql (pg_trgm)
-- ============================================================================

-- Coluna gerada (tsvector) para busca full-text em português no nome do usuário.
-- Usada pela Busca Global (Ctrl+K) — ver docs/requisitos/02-requisitos-funcionais-core.md.
ALTER TABLE users
  ADD COLUMN IF NOT EXISTS search_vector tsvector
  GENERATED ALWAYS AS (to_tsvector('portuguese', name)) STORED;

CREATE INDEX IF NOT EXISTS ix_users_search
  ON users USING GIN (search_vector);

-- Busca fuzzy (tolerante a erro de digitação) pelo nome do usuário.
CREATE INDEX IF NOT EXISTS ix_users_name_trgm
  ON users USING GIN (name gin_trgm_ops);

-- BRIN é ideal para colunas que crescem monotonicamente com o tempo (created_at de
-- audit_logs, que só cresce e nunca é atualizado) — muito mais compacto que um B-tree
-- comum em tabelas grandes, ao custo de scans um pouco menos seletivos.
CREATE INDEX IF NOT EXISTS ix_audit_logs_created_brin
  ON audit_logs USING BRIN (created_at);
