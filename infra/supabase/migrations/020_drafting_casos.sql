-- ============================================================================
-- 020_drafting_casos.sql — RLS e grants do isolamento por caso, revisão humana e
-- eventos de segurança do módulo Drafting (ADR-074).
--
-- Tabelas (criadas pela migration CasosAnexosRevisao do DraftingDbContext):
--   casos · caso_membros · caso_documentos · caso_documento_chunks
--   legal_document_citations · drafting_security_events
--
-- Pré-requisitos: 016_drafting.sql, 017_normas_juridicas.sql e a migration do EF Core.
--
-- O RLS isola ESCRITÓRIOS. O isolamento entre CASOS do mesmo escritório é da aplicação
-- (CasoAcessoService + filtro obrigatório de caso no índice vetorial): o banco não conhece
-- o usuário da requisição, só o tenant.
--
-- Texto de anexo, fatos, minuta e citações são cifrados pela aplicação (AES-256-GCM) antes de
-- chegar aqui; o arquivo original e o Markdown ficam, também cifrados, no object storage.
--
-- ATENÇÃO: o RLS do Postgres NÃO protege o Qdrant. Lá o isolamento é o filtro tenantId + casoId
-- que o CasoQdrantStore aplica em toda leitura e toda exclusão.
-- ============================================================================

ALTER TABLE casos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_casos ON casos
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_casos_write ON casos
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE caso_membros ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_caso_membros ON caso_membros
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_caso_membros_write ON caso_membros
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE caso_documentos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_caso_documentos ON caso_documentos
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_caso_documentos_write ON caso_documentos
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE caso_documento_chunks ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_caso_documento_chunks ON caso_documento_chunks
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_caso_documento_chunks_write ON caso_documento_chunks
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE legal_document_citations ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_legal_document_citations ON legal_document_citations
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_legal_document_citations_write ON legal_document_citations
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE drafting_security_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_drafting_security_events ON drafting_security_events
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_drafting_security_events_write ON drafting_security_events
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- Caso não é apagado por usuário (encerra-se). Anexos e trechos PODEM ser removidos (LGPD): a aplicação apaga
-- também o índice vetorial e o storage. Citações e eventos de segurança são trilha: sem DELETE.
GRANT SELECT, INSERT, UPDATE ON public.casos TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.caso_membros TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.caso_documentos TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.caso_documento_chunks TO app_user;
GRANT SELECT, INSERT, UPDATE ON public.legal_document_citations TO app_user;
GRANT SELECT, INSERT ON public.drafting_security_events TO app_user;

-- Busca por referência exata (norma + artigo), usada em toda geração.
CREATE INDEX IF NOT EXISTS ix_norma_chunks_artigo
    ON norma_chunks (norma_juridica_id, (upper(regexp_replace(artigo, '[.ºo°]', '', 'g'))));
CREATE INDEX IF NOT EXISTS ix_normas_juridicas_numero
    ON normas_juridicas ((replace(numero, '.', '')));
