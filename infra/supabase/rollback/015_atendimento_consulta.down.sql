-- Reverte 015_atendimento_consulta.sql
DROP INDEX IF EXISTS ix_clientes_tenant_telefone;

-- Volta ao estado que as default privileges de 002_rls.sql dariam a uma tabela nova.
GRANT SELECT, INSERT, UPDATE, DELETE ON public.atendimento_consulta_sessoes TO app_user;

DROP POLICY IF EXISTS tenant_isolation_atendimento_consulta_sessoes_write ON atendimento_consulta_sessoes;
DROP POLICY IF EXISTS tenant_isolation_atendimento_consulta_sessoes ON atendimento_consulta_sessoes;
ALTER TABLE atendimento_consulta_sessoes DISABLE ROW LEVEL SECURITY;
