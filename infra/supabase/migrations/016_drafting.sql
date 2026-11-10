-- ============================================================================
-- 016_drafting.sql — RLS das tabelas do módulo Drafting (geração assistida de peças,
-- ADR-072): legal_document_requests, retrieved_sources e legal_document_events.
--
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration do EF Core do DraftingDbContext, que cria as três tabelas. Gerar com:
--       dotnet ef migrations add InitialDrafting \
--         --project src/Modules/Drafting/Drafting.Infrastructure \
--         --startup-project src/Hosts/Api --context DraftingDbContext \
--         --output-dir Persistence/Migrations
--
-- NULLIF(..., '') e WITH CHECK separado no INSERT: mesmos motivos de 012_clientes_processos.sql.
--
-- ATENÇÃO: o RLS do Postgres NÃO protege o banco vetorial (Qdrant). O isolamento lá é o filtro
-- obrigatório por `tenantId` do QdrantService — ver ADR-072.
-- ============================================================================

ALTER TABLE legal_document_requests ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_legal_document_requests ON legal_document_requests
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_legal_document_requests_write ON legal_document_requests
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE retrieved_sources ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_retrieved_sources ON retrieved_sources
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_retrieved_sources_write ON retrieved_sources
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE legal_document_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_legal_document_events ON legal_document_events
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_legal_document_events_write ON legal_document_events
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- Sem DELETE para a aplicação: solicitações, fontes e eventos são trilha de uso de IA e não são
-- apagados por usuário (a exclusão por pedido do titular, LGPD, é rotina de DBA com registro).
GRANT SELECT, INSERT, UPDATE ON public.legal_document_requests TO app_user;
GRANT SELECT, INSERT ON public.retrieved_sources TO app_user;
GRANT SELECT, INSERT ON public.legal_document_events TO app_user;
