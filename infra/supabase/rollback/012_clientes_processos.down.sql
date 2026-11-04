-- Reverte 012_clientes_processos.sql
DROP INDEX IF EXISTS ix_processos_parte_contraria_trgm;
DROP INDEX IF EXISTS ix_processos_cnj_digits_trgm;
DROP INDEX IF EXISTS ix_clientes_nome_trgm;

REVOKE SELECT, INSERT, UPDATE, DELETE ON public.processos FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.clientes FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_processos_write ON processos;
DROP POLICY IF EXISTS tenant_isolation_processos ON processos;
ALTER TABLE processos DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_clientes_write ON clientes;
DROP POLICY IF EXISTS tenant_isolation_clientes ON clientes;
ALTER TABLE clientes DISABLE ROW LEVEL SECURITY;
