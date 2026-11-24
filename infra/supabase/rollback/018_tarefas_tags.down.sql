-- Reverte 018_tarefas_tags.sql
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.work_tasks FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.tags FROM app_user;
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.tag_assignments FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_tag_assignments_write ON tag_assignments;
DROP POLICY IF EXISTS tenant_isolation_tag_assignments ON tag_assignments;
ALTER TABLE tag_assignments DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_tags_write ON tags;
DROP POLICY IF EXISTS tenant_isolation_tags ON tags;
ALTER TABLE tags DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_work_tasks_write ON work_tasks;
DROP POLICY IF EXISTS tenant_isolation_work_tasks ON work_tasks;
ALTER TABLE work_tasks DISABLE ROW LEVEL SECURITY;
