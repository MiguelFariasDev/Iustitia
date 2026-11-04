-- Reverte 014_publication_suggestions.sql (RLS, constraints e grants).
-- NÃO remove as tabelas: elas pertencem à migration AddPublicationSuggestionsAndAiUsageLogs do
-- EF Core (reverter com `dotnet ef database update <migration anterior>`).
REVOKE SELECT, INSERT ON public.ai_usage_logs FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.publication_suggestions FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_ai_usage_logs_write ON ai_usage_logs;
DROP POLICY IF EXISTS tenant_isolation_ai_usage_logs ON ai_usage_logs;
ALTER TABLE ai_usage_logs DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_publication_suggestions_write ON publication_suggestions;
DROP POLICY IF EXISTS tenant_isolation_publication_suggestions ON publication_suggestions;
ALTER TABLE publication_suggestions DISABLE ROW LEVEL SECURITY;

ALTER TABLE ai_usage_logs DROP CONSTRAINT IF EXISTS ck_ai_usage_logs_non_negative;
ALTER TABLE ai_usage_logs DROP CONSTRAINT IF EXISTS ck_ai_usage_logs_status;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_days;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_pair;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_type;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_confidence;
