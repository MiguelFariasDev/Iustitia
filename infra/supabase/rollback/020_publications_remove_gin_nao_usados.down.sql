-- Recria o que 020 removeu (mesmas definições de 010). Reconstruir a coluna gerada reescreve
-- a tabela inteira: fazer em janela de baixo tráfego.
ALTER TABLE publications
  ADD COLUMN IF NOT EXISTS search_vector tsvector
  GENERATED ALWAYS AS (to_tsvector('portuguese', raw_content)) STORED;
CREATE INDEX IF NOT EXISTS ix_publications_search ON publications USING GIN (search_vector);
CREATE INDEX IF NOT EXISTS ix_publications_raw_content_trgm ON publications USING GIN (raw_content gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_publications_cnj_number_trgm ON publications USING GIN (cnj_number gin_trgm_ops);
