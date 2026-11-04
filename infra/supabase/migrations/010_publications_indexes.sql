-- ============================================================================
-- 010_publications_indexes.sql — Índices da tabela publications que o EF Core
-- não consegue expressar em Fluent API: GIN (full-text e trigram) e BRIN.
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 000_extensions.sql (pg_trgm)
--   - Migration InitialCreate do LegalDbContext (cria a tabela publications)
-- Ver ADR-057 (Estratégia de Índices para Publications).
--
-- Os índices B-tree e parciais desta tabela NÃO estão aqui: são declarados em
-- PublicationConfiguration (Fluent API) e criados pela migration do EF Core, que é
-- a fonte única de verdade deles. Duplicá-los aqui criaria duas definições
-- divergentes do mesmo índice. Ver o rodapé deste arquivo para a lista completa.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Busca full-text em português no inteiro teor da publicação.
--
-- Coluna gerada STORED (e não um índice de expressão sobre to_tsvector): o
-- tsvector de um texto de dezenas de KB é caro de recalcular, e como STORED ele é
-- computado uma única vez na escrita. A publicação é imutável depois de capturada,
-- então esse custo é pago exatamente uma vez por linha.
--
-- O trade-off é espaço: o tsvector chega a ~30% do tamanho do texto. Aceitável
-- porque a busca por conteúdo de publicação é uma funcionalidade central (Busca
-- Global / Ctrl+K) e um seq scan em uma tabela que cresce indefinidamente não é.
-- ----------------------------------------------------------------------------
ALTER TABLE publications
  ADD COLUMN IF NOT EXISTS search_vector tsvector
  GENERATED ALWAYS AS (to_tsvector('portuguese', raw_content)) STORED;

CREATE INDEX IF NOT EXISTS ix_publications_search
  ON publications USING GIN (search_vector);

-- ----------------------------------------------------------------------------
-- Busca fuzzy (tolerante a erro de digitação e a busca por trecho parcial) no
-- conteúdo. Complementa o full-text: to_tsvector casa palavras inteiras após
-- stemming, o trigram casa pedaços — necessário para buscar por um fragmento de
-- nome de parte ou por um número citado no meio do texto.
--
-- gin_trgm_ops em uma coluna text grande é o índice mais caro desta tabela em
-- escrita e espaço. Mantido porque a alternativa (ILIKE '%...%' sem índice) é um
-- seq scan com leitura de todo o raw_content da tabela.
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_publications_raw_content_trgm
  ON publications USING GIN (raw_content gin_trgm_ops);

-- Busca fuzzy pelo número do processo (usuário digita com máscara parcial, com
-- pontuação diferente, ou só o sequencial).
CREATE INDEX IF NOT EXISTS ix_publications_cnj_number_trgm
  ON publications USING GIN (cnj_number gin_trgm_ops);

-- ----------------------------------------------------------------------------
-- BRIN em published_at e captured_at: as duas colunas crescem de forma quase
-- monotônica (publicações entram em ordem de diário) e a tabela é append-only na
-- prática. BRIN ocupa alguns KB onde um B-tree ocuparia centenas de MB, ao custo
-- de scans menos seletivos — troca certa para filtros por FAIXA de data
-- ("publicações dos últimos 30 dias"), que é o padrão de relatório e de expurgo.
--
-- Não substitui ix_publications_tenant_process_published (B-tree), que serve a
-- ordenação por data DENTRO de um processo — BRIN não ordena.
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_publications_published_brin
  ON publications USING BRIN (published_at);

CREATE INDEX IF NOT EXISTS ix_publications_captured_brin
  ON publications USING BRIN (captured_at);

-- ----------------------------------------------------------------------------
-- Autovacuum mais agressivo que o padrão (ver 003_autovacuum.sql para o mesmo
-- tratamento em audit_logs): publications recebe inserções em rajada (milhares por
-- execução do job de captura) e updates esparsos de revisão. Sem isso, os índices
-- GIN acumulam bloat entre vacuums e a tabela passa a crescer mais rápido que o
-- necessário.
-- ----------------------------------------------------------------------------
ALTER TABLE publications SET (
  autovacuum_vacuum_scale_factor = 0.05,
  autovacuum_analyze_scale_factor = 0.02
);

-- ============================================================================
-- Índices desta tabela criados pela migration do EF Core (NÃO redeclarar aqui):
--   ux_publications_tenant_external_id   UNIQUE (tenant_id, external_id, source)
--                                        WHERE is_deleted = false  -- deduplicação
--   ix_publications_tenant_process_published (tenant_id, process_id,
--                                        published_at DESC)        -- timeline
--   ix_publications_tenant_status        (tenant_id, status)
--   ix_publications_tenant_pending_review (tenant_id, process_id)
--                                        WHERE status = 'PendingReview'  -- fila
--   ix_publications_tenant_unread        (tenant_id, is_read)
--                                        WHERE is_read = false     -- não lidas
--   ix_publications_cnj_number           (cnj_number)
-- ============================================================================
