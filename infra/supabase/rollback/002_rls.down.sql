-- Reverte 002_rls.sql
--
-- PERIGO: sem RLS, a role app_user enxerga dados de TODOS os tenants (ADR-004,
-- princípio inegociável de isolamento). Só execute como passo intermediário de
-- uma correção, com a aplicação fora do ar, e reaplique 002_rls.sql em seguida.
-- Exige confirmação explícita na mesma sessão:
--   SET advocacia.confirm_disable_rls = 'yes';

DO $$
BEGIN
  IF coalesce(current_setting('advocacia.confirm_disable_rls', true), '') <> 'yes' THEN
    RAISE EXCEPTION 'Rollback de RLS bloqueado: execute SET advocacia.confirm_disable_rls = ''yes'' na mesma sessão para confirmar.';
  END IF;
END
$$;

DROP POLICY IF EXISTS tenant_isolation_feature_flags ON feature_flags;
DROP POLICY IF EXISTS tenant_isolation_settings ON settings;
DROP POLICY IF EXISTS tenant_isolation_audit_logs ON audit_logs;
DROP POLICY IF EXISTS tenant_isolation_users ON users;
DROP POLICY IF EXISTS tenant_isolation_tenants ON tenants;

ALTER TABLE feature_flags DISABLE ROW LEVEL SECURITY;
ALTER TABLE settings DISABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs DISABLE ROW LEVEL SECURITY;
ALTER TABLE users DISABLE ROW LEVEL SECURITY;
ALTER TABLE tenants DISABLE ROW LEVEL SECURITY;

-- A role app_user e seus grants são mantidos: removê-los derrubaria a aplicação,
-- e reaplicar 002_rls.sql é idempotente em relação a eles.
