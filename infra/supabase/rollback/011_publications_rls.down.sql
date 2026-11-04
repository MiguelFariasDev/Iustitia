-- Reverte 011_publications_rls.sql
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.publications FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_publications_write ON publications;
DROP POLICY IF EXISTS tenant_isolation_publications ON publications;

ALTER TABLE publications DISABLE ROW LEVEL SECURITY;
