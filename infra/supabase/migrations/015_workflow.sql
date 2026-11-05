-- ============================================================================
-- 015_workflow.sql — RLS das tabelas do módulo Workflow (work_tasks e calendar_events).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration InitialCreate do WorkflowDbContext (cria as duas tabelas e os índices)
-- Ver ADR-004 (RLS) e ADR-070 (módulo Workflow).
--
-- As policies isolam por TENANT. O isolamento por USUÁRIO (cada um vê só as suas tarefas e os
-- seus compromissos) é feito pela aplicação — os repositórios filtram tenant E usuário —, porque a
-- API conecta como app_user e o banco só conhece app.current_tenant (mesma decisão de alerts, 013).
--
-- processo_id NÃO é chave estrangeira: o Workflow não conhece o módulo Legal (ADR-001), então é só
-- uma referência. Um processo apagado deixa a referência órfã, e a tela precisa tolerar isso.
-- ============================================================================

-- NULLIF(..., '') e WITH CHECK separado: mesmas razões de 011_publications_rls.sql.
ALTER TABLE work_tasks ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_work_tasks ON work_tasks
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_work_tasks_write ON work_tasks
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.work_tasks TO app_user;

ALTER TABLE calendar_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_calendar_events ON calendar_events
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_calendar_events_write ON calendar_events
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.calendar_events TO app_user;
