-- Reverte 014_publication_suggestions.sql (RLS, constraints e grants).
-- NÃO remove a tabela: ela pertence à migration AddPublicationSuggestions do EF Core
-- (reverter com `dotnet ef database update <migration anterior>`).
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.publication_suggestions FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_publication_suggestions_write ON publication_suggestions;
DROP POLICY IF EXISTS tenant_isolation_publication_suggestions ON publication_suggestions;
ALTER TABLE publication_suggestions DISABLE ROW LEVEL SECURITY;

ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_days;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_pair;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_deadline_type;
ALTER TABLE publication_suggestions DROP CONSTRAINT IF EXISTS ck_publication_suggestions_confidence;
