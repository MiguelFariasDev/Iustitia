-- Reverte 016_drafting.sql
REVOKE SELECT, INSERT, UPDATE ON public.legal_document_requests FROM app_user;
REVOKE SELECT, INSERT ON public.retrieved_sources FROM app_user;
REVOKE SELECT, INSERT ON public.legal_document_events FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.indexed_documents FROM app_user;
REVOKE SELECT, INSERT, UPDATE ON public.generation_queue FROM app_user;
REVOKE SELECT, INSERT ON public.gpu_instance_logs FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_indexed_documents_write ON indexed_documents;
DROP POLICY IF EXISTS tenant_isolation_indexed_documents ON indexed_documents;
ALTER TABLE indexed_documents DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_legal_document_events_write ON legal_document_events;
DROP POLICY IF EXISTS tenant_isolation_legal_document_events ON legal_document_events;
ALTER TABLE legal_document_events DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_retrieved_sources_write ON retrieved_sources;
DROP POLICY IF EXISTS tenant_isolation_retrieved_sources ON retrieved_sources;
ALTER TABLE retrieved_sources DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_legal_document_requests_write ON legal_document_requests;
DROP POLICY IF EXISTS tenant_isolation_legal_document_requests ON legal_document_requests;
ALTER TABLE legal_document_requests DISABLE ROW LEVEL SECURITY;
