-- Validação de RLS pós-migration (rodada pelo cd-supabase.yml; também útil à mão):
--   psql "$SUPABASE_DB_URL" -v ON_ERROR_STOP=1 -f scripts/validate-rls.sql
--
-- Falha (RAISE EXCEPTION) se:
--   1. alguma tabela de public com coluna tenant_id (ou a própria tenants) não tem
--      RLS habilitado ou não tem nenhuma policy;
--   2. a role da aplicação (app_user) pode ignorar RLS (superuser/BYPASSRLS) ou é
--      dona de alguma tabela de negócio (donos ignoram RLS sem FORCE).
-- Tabelas técnicas globais sem tenant_id (outbox_messages, processed_events,
-- __EFMigrationsHistory, hangfire.*) ficam de fora por definição (ver 002_rls.sql).

DO $$
DECLARE
  problems text := '';
  rec record;
BEGIN
  FOR rec IN
    SELECT c.relname,
           c.relrowsecurity,
           (SELECT count(*) FROM pg_policies p WHERE p.schemaname = 'public' AND p.tablename = c.relname) AS policy_count
      FROM pg_class c
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = 'public'
       AND c.relkind IN ('r', 'p')
       AND (c.relname = 'tenants'
            OR EXISTS (SELECT 1 FROM information_schema.columns col
                        WHERE col.table_schema = 'public'
                          AND col.table_name = c.relname
                          AND col.column_name = 'tenant_id'))
  LOOP
    IF NOT rec.relrowsecurity THEN
      problems := problems || format(E'\n  - %s: RLS desabilitado', rec.relname);
    ELSIF rec.policy_count = 0 THEN
      problems := problems || format(E'\n  - %s: RLS habilitado mas sem nenhuma policy', rec.relname);
    END IF;
  END LOOP;

  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_user' AND (rolsuper OR rolbypassrls)) THEN
    problems := problems || E'\n  - app_user é superuser ou tem BYPASSRLS';
  END IF;

  FOR rec IN
    SELECT c.relname
      FROM pg_class c
      JOIN pg_namespace n ON n.oid = c.relnamespace
      JOIN pg_roles r ON r.oid = c.relowner
     WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND r.rolname = 'app_user'
  LOOP
    problems := problems || format(E'\n  - %s: tabela pertence a app_user (dono ignora RLS)', rec.relname);
  END LOOP;

  IF problems <> '' THEN
    RAISE EXCEPTION 'Validação de RLS falhou:%', problems;
  END IF;

  RAISE NOTICE 'RLS OK: todas as tabelas multi-tenant têm RLS + policy, e app_user não ignora RLS.';
END
$$;
