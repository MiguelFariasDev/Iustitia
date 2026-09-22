-- ============================================================================
-- 002_rls.sql — Row-Level Security, policies de isolamento por tenant e role
-- de aplicação com permissões restritas.
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Ver ADR-004 (Row-Level Security para multi-tenancy).
-- ============================================================================

-- RLS não se aplica ao dono da tabela nem a superusers por padrão — a role da
-- aplicação (app_user, criada abaixo) precisa ser diferente do dono das tabelas
-- (postgres/service_role) para que as policies sejam realmente aplicadas. Em dev
-- local a app pode conectar como postgres por conveniência, mas nesse caso o RLS
-- não é exercido — os testes de isolamento (TenantIsolationTests) conectam
-- explicitamente como app_user para validar as policies de verdade.

ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE feature_flags ENABLE ROW LEVEL SECURITY;
-- outbox_messages não recebe RLS: é uma tabela técnica global, sem tenant_id
-- (ver OutboxMessageConfiguration).

-- current_setting(..., true) com missing_ok=true: se app.current_tenant não foi
-- definido na sessão (via TenantContextInterceptor.SET), retorna NULL em vez de
-- lançar erro — e comparação com NULL nunca é verdadeira, então a policy nega por
-- padrão (fail closed) em vez de vazar dados por esquecimento de configurar o tenant.

CREATE POLICY tenant_isolation_tenants ON tenants
  USING (id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY tenant_isolation_users ON users
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY tenant_isolation_audit_logs ON audit_logs
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY tenant_isolation_settings ON settings
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

CREATE POLICY tenant_isolation_feature_flags ON feature_flags
  USING (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- Role de aplicação com permissões restritas (sem CREATEDB/CREATEROLE/SUPERUSER).
-- A senha é definida fora deste script (Azure Key Vault em produção; variável local
-- em dev/CI) — nunca commitar credenciais. CREATE ROLE não suporta IF NOT EXISTS,
-- daí o bloco DO abaixo para manter o script idempotente.
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'app_user') THEN
    CREATE ROLE app_user WITH LOGIN;
  END IF;
END
$$;

GRANT USAGE ON SCHEMA public TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO app_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO app_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO app_user;
