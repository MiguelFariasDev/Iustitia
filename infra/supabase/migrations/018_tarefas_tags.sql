-- ============================================================================
-- 018_tarefas_tags.sql — RLS de work_tasks, tags e tag_assignments (módulo Workflow).
--
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration do EF Core AddTasksAndTags (WorkflowDbContext).
-- Mesmo padrão de 014_calendar_contacts.sql: NULLIF(..., '') e WITH CHECK separado no INSERT.
-- ============================================================================

ALTER TABLE work_tasks ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_work_tasks ON work_tasks
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_work_tasks_write ON work_tasks
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE tags ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_tags ON tags
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_tags_write ON tags
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE tag_assignments ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_tag_assignments ON tag_assignments
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_tag_assignments_write ON tag_assignments
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.work_tasks TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.tags TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.tag_assignments TO app_user;
