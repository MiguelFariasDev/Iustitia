-- ============================================================================
-- 014_audit_immutability.sql — audit_logs imutável, arquivamento em storage frio e
-- retenção mínima de 5 anos IMPOSTA PELO BANCO (Etapa 3.9, ADR-069).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos: 002_rls.sql (role app_user, RLS) e a migration do EF que cria audit_logs.
--
-- Camadas (defesa em profundidade — qualquer uma sozinha já segura a aplicação):
--   1. PRIVILÉGIOS: app_user só tem SELECT e INSERT em audit_logs (sem UPDATE/DELETE/TRUNCATE).
--   2. RLS: só policies de SELECT e INSERT; sem policy de UPDATE/DELETE, as linhas são intocáveis.
--   3. TRIGGER: bloqueia UPDATE e TRUNCATE sempre — até para o dono da tabela. DELETE só passa
--      de dentro das funções de manutenção abaixo E apenas para linhas (a) já arquivadas em
--      storage frio (manifesto em audit_log_archives) ou (b) com mais de 5 anos.
--
-- O prazo de 5 anos e a regra "só apaga o que está arquivado" vivem AQUI, em SQL, e não em
-- appsettings: uma configuração errada na aplicação não consegue violar a retenção legal.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1) RLS: SELECT e INSERT apenas (substitui a policy FOR ALL de 002_rls.sql)
-- ----------------------------------------------------------------------------
DROP POLICY IF EXISTS tenant_isolation_audit_logs ON audit_logs;
DROP POLICY IF EXISTS audit_logs_select ON audit_logs;
DROP POLICY IF EXISTS audit_logs_insert ON audit_logs;

CREATE POLICY audit_logs_select ON audit_logs
  FOR SELECT
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY audit_logs_insert ON audit_logs
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- ----------------------------------------------------------------------------
-- 2) Privilégios
-- ----------------------------------------------------------------------------
REVOKE ALL ON audit_logs FROM app_user;
GRANT SELECT, INSERT ON audit_logs TO app_user;

-- Os papéis do Supabase (PostgREST) não têm por que enxergar auditoria: ela só é acessada
-- pelo backend .NET. Os papéis podem não existir fora do Supabase (dev/CI).
DO $$
DECLARE r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = r) THEN
      EXECUTE format('REVOKE ALL ON audit_logs FROM %I', r);
    END IF;
  END LOOP;
END
$$;

-- ----------------------------------------------------------------------------
-- 3) Manifesto de arquivamento: um registro por (tenant, mês) já gravado no storage frio.
--    Imutável também. app_user NÃO acessa direto: só pelas funções SECURITY DEFINER.
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit_log_archives (
  id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
  tenant_id     uuid        NOT NULL,
  period_month  date        NOT NULL,           -- primeiro dia do mês (UTC)
  row_count     integer     NOT NULL CHECK (row_count > 0),
  sha256        text        NOT NULL,           -- hash do arquivo gravado no storage
  blob_uri      text        NOT NULL,
  archived_at   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ux_audit_log_archives_tenant_month UNIQUE (tenant_id, period_month)
);

ALTER TABLE audit_log_archives ENABLE ROW LEVEL SECURITY;   -- sem policy: nega tudo a quem não é dono
REVOKE ALL ON audit_log_archives FROM PUBLIC;
REVOKE ALL ON audit_log_archives FROM app_user;
DO $$
DECLARE r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
    IF EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = r) THEN
      EXECUTE format('REVOKE ALL ON audit_log_archives FROM %I', r);
    END IF;
  END LOOP;
END
$$;

-- ----------------------------------------------------------------------------
-- 4) Triggers de imutabilidade
-- ----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION audit_logs_guard_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  -- DELETE só dentro das funções de manutenção (que ligam a flag local à transação) E só
  -- para o que já está seguro no storage frio ou passou dos 5 anos.
  IF TG_OP = 'DELETE'
     AND current_setting('app.audit_maintenance', true) = 'on'
     AND (
       OLD.created_at < now() - interval '5 years'
       OR EXISTS (
         SELECT 1 FROM audit_log_archives a
         WHERE a.tenant_id = OLD.tenant_id
           AND a.period_month = date_trunc('month', OLD.created_at AT TIME ZONE 'UTC')::date
       )
     )
  THEN
    RETURN OLD;
  END IF;

  RAISE EXCEPTION 'audit_logs é imutável: % bloqueado', TG_OP
    USING ERRCODE = 'insufficient_privilege';
END
$$;

CREATE OR REPLACE FUNCTION audit_logs_guard_truncate() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'audit_logs é imutável: TRUNCATE bloqueado'
    USING ERRCODE = 'insufficient_privilege';
END
$$;

CREATE OR REPLACE FUNCTION audit_log_archives_guard_mutation() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'audit_log_archives é imutável: % bloqueado', TG_OP
    USING ERRCODE = 'insufficient_privilege';
END
$$;

DROP TRIGGER IF EXISTS audit_logs_no_update ON audit_logs;
DROP TRIGGER IF EXISTS audit_logs_no_delete ON audit_logs;
DROP TRIGGER IF EXISTS audit_logs_no_truncate ON audit_logs;
DROP TRIGGER IF EXISTS audit_log_archives_no_change ON audit_log_archives;

CREATE TRIGGER audit_logs_no_update BEFORE UPDATE ON audit_logs
  FOR EACH ROW EXECUTE FUNCTION audit_logs_guard_mutation();
CREATE TRIGGER audit_logs_no_delete BEFORE DELETE ON audit_logs
  FOR EACH ROW EXECUTE FUNCTION audit_logs_guard_mutation();
CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON audit_logs
  FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_guard_truncate();
CREATE TRIGGER audit_log_archives_no_change BEFORE UPDATE OR DELETE ON audit_log_archives
  FOR EACH ROW EXECUTE FUNCTION audit_log_archives_guard_mutation();

-- ----------------------------------------------------------------------------
-- 5) Funções de manutenção (SECURITY DEFINER, search_path fixo)
--
-- Rodam como o DONO das tabelas — por isso enxergam todos os tenants apesar do RLS — mas só
-- fazem o que está escrito aqui. O limite de idade (meses COMPLETOS com mais de 1 ano) é
-- calculado dentro do banco: o chamador não consegue pedir para arquivar/ler dado recente.
-- ----------------------------------------------------------------------------

-- Início (UTC) do mês que contém "agora - 1 ano": tudo ANTES disso são meses completos > 1 ano.
CREATE OR REPLACE FUNCTION audit_archive_cutoff() RETURNS timestamptz
LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT date_trunc('month', (now() AT TIME ZONE 'UTC') - interval '1 year') AT TIME ZONE 'UTC'
$$;

CREATE OR REPLACE FUNCTION audit_archive_candidates(p_limit integer)
RETURNS TABLE (tenant_id uuid, period_month date, row_count bigint)
LANGUAGE sql SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT l.tenant_id,
         date_trunc('month', l.created_at AT TIME ZONE 'UTC')::date AS period_month,
         count(*) AS row_count
  FROM audit_logs l
  WHERE l.created_at < audit_archive_cutoff()
    AND NOT EXISTS (
      SELECT 1 FROM audit_log_archives a
      WHERE a.tenant_id = l.tenant_id
        AND a.period_month = date_trunc('month', l.created_at AT TIME ZONE 'UTC')::date)
  GROUP BY l.tenant_id, date_trunc('month', l.created_at AT TIME ZONE 'UTC')::date
  ORDER BY 2, 1
  LIMIT GREATEST(p_limit, 1)
$$;

CREATE OR REPLACE FUNCTION audit_archive_read(p_tenant uuid, p_month date)
RETURNS SETOF audit_logs
LANGUAGE sql SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT l.*
  FROM audit_logs l
  WHERE l.tenant_id = p_tenant
    AND l.created_at >= (p_month::timestamp AT TIME ZONE 'UTC')
    AND l.created_at <  ((p_month + interval '1 month')::timestamp AT TIME ZONE 'UTC')
    AND l.created_at <  audit_archive_cutoff()
  ORDER BY l.created_at, l.id
$$;

-- Registra o arquivo no manifesto e remove do banco as linhas do mês — na MESMA transação, e só
-- se a contagem bater com o que foi gravado no storage (nada é apagado sem estar arquivado).
CREATE OR REPLACE FUNCTION audit_archive_commit(
  p_tenant uuid, p_month date, p_row_count integer, p_sha256 text, p_blob_uri text)
RETURNS integer
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_start   timestamptz := (p_month::timestamp AT TIME ZONE 'UTC');
  v_end     timestamptz := ((p_month + interval '1 month')::timestamp AT TIME ZONE 'UTC');
  v_count   integer;
  v_deleted integer;
BEGIN
  IF v_end > audit_archive_cutoff() THEN
    RAISE EXCEPTION 'mês % ainda não pode ser arquivado (menos de 1 ano)', p_month;
  END IF;

  SELECT count(*) INTO v_count FROM audit_logs
  WHERE tenant_id = p_tenant AND created_at >= v_start AND created_at < v_end;

  IF v_count <> p_row_count THEN
    RAISE EXCEPTION 'contagem divergente para % / %: banco=% arquivo=%', p_tenant, p_month, v_count, p_row_count;
  END IF;

  INSERT INTO audit_log_archives (tenant_id, period_month, row_count, sha256, blob_uri)
  VALUES (p_tenant, p_month, p_row_count, p_sha256, p_blob_uri)
  ON CONFLICT ON CONSTRAINT ux_audit_log_archives_tenant_month DO NOTHING;

  PERFORM set_config('app.audit_maintenance', 'on', true);   -- local à transação
  DELETE FROM audit_logs WHERE tenant_id = p_tenant AND created_at >= v_start AND created_at < v_end;
  GET DIAGNOSTICS v_deleted = ROW_COUNT;
  PERFORM set_config('app.audit_maintenance', 'off', true);

  RETURN v_deleted;
END
$$;

-- Retenção: remove o que passou de 5 anos (o trigger valida a idade mesmo assim).
CREATE OR REPLACE FUNCTION audit_purge_expired() RETURNS bigint
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_deleted bigint;
BEGIN
  PERFORM set_config('app.audit_maintenance', 'on', true);
  DELETE FROM audit_logs WHERE created_at < now() - interval '5 years';
  GET DIAGNOSTICS v_deleted = ROW_COUNT;
  PERFORM set_config('app.audit_maintenance', 'off', true);
  RETURN v_deleted;
END
$$;

REVOKE ALL ON FUNCTION audit_archive_cutoff() FROM PUBLIC;
REVOKE ALL ON FUNCTION audit_archive_candidates(integer) FROM PUBLIC;
REVOKE ALL ON FUNCTION audit_archive_read(uuid, date) FROM PUBLIC;
REVOKE ALL ON FUNCTION audit_archive_commit(uuid, date, integer, text, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION audit_purge_expired() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION audit_archive_cutoff() TO app_user;
GRANT EXECUTE ON FUNCTION audit_archive_candidates(integer) TO app_user;
GRANT EXECUTE ON FUNCTION audit_archive_read(uuid, date) TO app_user;
GRANT EXECUTE ON FUNCTION audit_archive_commit(uuid, date, integer, text, text) TO app_user;
GRANT EXECUTE ON FUNCTION audit_purge_expired() TO app_user;
