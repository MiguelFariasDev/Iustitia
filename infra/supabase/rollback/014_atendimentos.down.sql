-- Reverte 014_atendimentos.sql
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'realtime') THEN
    DROP POLICY IF EXISTS atendimentos_realtime_receive ON realtime.messages;
  END IF;
END
$$;

DROP INDEX IF EXISTS ix_atendimentos_phone_trgm;
DROP INDEX IF EXISTS ix_atendimentos_contact_name_trgm;

-- Volta ao estado que as default privileges de 002_rls.sql dariam a uma tabela nova.
GRANT SELECT, INSERT, UPDATE, DELETE ON public.bot_api_keys TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.atendimento_auditoria TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.atendimento_mensagens TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.atendimentos TO app_user;

DROP POLICY IF EXISTS tenant_isolation_atendimento_auditoria_write ON atendimento_auditoria;
DROP POLICY IF EXISTS tenant_isolation_atendimento_auditoria ON atendimento_auditoria;
ALTER TABLE atendimento_auditoria DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_atendimento_mensagens_write ON atendimento_mensagens;
DROP POLICY IF EXISTS tenant_isolation_atendimento_mensagens ON atendimento_mensagens;
ALTER TABLE atendimento_mensagens DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_atendimentos_write ON atendimentos;
DROP POLICY IF EXISTS tenant_isolation_atendimentos ON atendimentos;
ALTER TABLE atendimentos DISABLE ROW LEVEL SECURITY;
