-- ============================================================================
-- 014_calendar_contacts.sql — RLS e índices avançados de contacts (CRM) e
-- calendar_events (Workflow). Etapas 4.1 e 4.2.
--
-- Pré-requisitos:
--   - 000_extensions.sql (pg_trgm), 002_rls.sql (role app_user)
--   - Migrations do EF Core: CrmDbContext (tabela contacts) e WorkflowDbContext
--     (tabela calendar_events). Ver docs/modules/agenda-contatos.md.
-- Ver ADR-004 (RLS) e ADR-067 (agenda e contatos).
--
-- NULLIF(..., '') e WITH CHECK separado no INSERT: mesmos motivos de 012_clientes_processos.sql.
-- ============================================================================

ALTER TABLE contacts ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_contacts ON contacts
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_contacts_write ON contacts
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE calendar_events ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_calendar_events ON calendar_events
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_calendar_events_write ON calendar_events
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.contacts TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.calendar_events TO app_user;

-- Busca fuzzy por nome de contato (pg_trgm sobre a coluna derivada em minúsculas).
CREATE INDEX IF NOT EXISTS ix_contacts_name_trgm
  ON contacts USING GIN (name_search gin_trgm_ops);

-- Unicidade parcial de documento por escritório NÃO é imposta de propósito: a mesma parte
-- contrária pode ser cadastrada como testemunha em outro caso, e a duplicata é tratada na tela.
