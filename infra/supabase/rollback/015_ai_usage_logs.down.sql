-- Reverte 015_ai_usage_logs.sql (RLS, constraints e grants).
-- NÃO remove a tabela: ela pertence à migration InitialCreate do AnthropicDbContext.
REVOKE SELECT, INSERT ON public.ai_usage_logs FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_ai_usage_logs_write ON ai_usage_logs;
DROP POLICY IF EXISTS tenant_isolation_ai_usage_logs ON ai_usage_logs;
ALTER TABLE ai_usage_logs DISABLE ROW LEVEL SECURITY;

ALTER TABLE ai_usage_logs DROP CONSTRAINT IF EXISTS ck_ai_usage_logs_error_code;
ALTER TABLE ai_usage_logs DROP CONSTRAINT IF EXISTS ck_ai_usage_logs_non_negative;
