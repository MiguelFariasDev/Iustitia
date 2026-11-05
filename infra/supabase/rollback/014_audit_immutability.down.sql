-- Reverte 014_audit_immutability.sql
-- ATENÇÃO: reverter REMOVE a proteção de imutabilidade da auditoria. Só em dev/staging.
-- O manifesto audit_log_archives é APAGADO: as linhas já movidas para o storage frio deixam de
-- ser rastreáveis pelo banco (os arquivos no Blob permanecem).

DROP FUNCTION IF EXISTS audit_purge_expired();
DROP FUNCTION IF EXISTS audit_archive_commit(uuid, date, integer, text, text);
DROP FUNCTION IF EXISTS audit_archive_read(uuid, date);
DROP FUNCTION IF EXISTS audit_archive_candidates(integer);
DROP FUNCTION IF EXISTS audit_archive_cutoff();

DROP TRIGGER IF EXISTS audit_logs_no_update ON audit_logs;
DROP TRIGGER IF EXISTS audit_logs_no_delete ON audit_logs;
DROP TRIGGER IF EXISTS audit_logs_no_truncate ON audit_logs;
DROP FUNCTION IF EXISTS audit_logs_guard_mutation();
DROP FUNCTION IF EXISTS audit_logs_guard_truncate();

DROP TABLE IF EXISTS audit_log_archives;
DROP FUNCTION IF EXISTS audit_log_archives_guard_mutation();

DROP POLICY IF EXISTS audit_logs_select ON audit_logs;
DROP POLICY IF EXISTS audit_logs_insert ON audit_logs;
CREATE POLICY tenant_isolation_audit_logs ON audit_logs
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON audit_logs TO app_user;
