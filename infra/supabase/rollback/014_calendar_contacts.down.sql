-- Reverte 014_calendar_contacts.sql
DROP INDEX IF EXISTS ix_contacts_name_trgm;

REVOKE SELECT, INSERT, UPDATE, DELETE ON public.contacts FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.calendar_events FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_calendar_events_write ON calendar_events;
DROP POLICY IF EXISTS tenant_isolation_calendar_events ON calendar_events;
ALTER TABLE calendar_events DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_contacts_write ON contacts;
DROP POLICY IF EXISTS tenant_isolation_contacts ON contacts;
ALTER TABLE contacts DISABLE ROW LEVEL SECURITY;
