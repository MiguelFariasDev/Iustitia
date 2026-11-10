-- ============================================================================
-- 016_drafting.sql — RLS e grants do módulo Drafting (geração assistida de peças
-- com fila, GPU sob demanda e acervo; ADR-072).
--
-- Tabelas (criadas pela migration do EF Core do DraftingDbContext):
--   legal_document_requests · retrieved_sources · legal_document_events   (dados do caso → RLS)
--   indexed_documents                                                     (acervo         → RLS)
--   generation_queue · gpu_instance_logs                                  (infraestrutura → SEM RLS)
--
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration do EF Core do DraftingDbContext. Gerar com:
--       dotnet ef migrations add InitialDrafting \
--         --project src/Modules/Drafting/Drafting.Infrastructure \
--         --startup-project src/Hosts/Api --context DraftingDbContext \
--         --output-dir Persistence/Migrations
--
-- NULLIF(..., '') e WITH CHECK separado no INSERT: mesmos motivos de 012_clientes_processos.sql.
--
-- POR QUE generation_queue E gpu_instance_logs NÃO TÊM RLS: o worker precisa enxergar a fila de TODOS
-- os escritórios para agrupar o trabalho e ligar a GPU uma única vez, e a GPU é infraestrutura
-- compartilhada. As duas tabelas só guardam ids, estados e carimbos de tempo — NENHUM dado do caso.
-- O dado do caso (fatos, pedidos, minuta, texto extraído) vive nas tabelas com RLS, que o worker só
-- acessa depois de definir o tenant do item. A API nunca expõe estas duas tabelas diretamente.
--
-- ATENÇÃO: o RLS do Postgres NÃO protege o banco vetorial (Qdrant). O isolamento lá é o filtro
-- obrigatório por `tenantId` (e a visibilidade do documento) do QdrantService — ver ADR-072.
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

ALTER TABLE indexed_documents ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_indexed_documents ON indexed_documents
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_indexed_documents_write ON indexed_documents
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- Sem DELETE para solicitações, fontes e eventos: são a trilha de uso de IA e não são apagados por
-- usuário (a exclusão por pedido do titular, LGPD, é rotina de DBA com registro). Documentos do acervo
-- PODEM ser removidos pelo usuário (e do índice vetorial).
GRANT SELECT, INSERT, UPDATE ON public.legal_document_requests TO app_user;
GRANT SELECT, INSERT ON public.retrieved_sources TO app_user;
GRANT SELECT, INSERT ON public.legal_document_events TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.indexed_documents TO app_user;
GRANT SELECT, INSERT, UPDATE ON public.generation_queue TO app_user;
GRANT SELECT, INSERT ON public.gpu_instance_logs TO app_user;
