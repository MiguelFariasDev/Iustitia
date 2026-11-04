-- Reverte 013_alerts.sql
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'realtime') THEN
    DROP POLICY IF EXISTS alerts_realtime_receive ON realtime.messages;
  END IF;
END
$$;

REVOKE SELECT, INSERT, UPDATE, DELETE ON public.alerts FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_alerts_write ON alerts;
DROP POLICY IF EXISTS tenant_isolation_alerts ON alerts;
ALTER TABLE alerts DISABLE ROW LEVEL SECURITY;
