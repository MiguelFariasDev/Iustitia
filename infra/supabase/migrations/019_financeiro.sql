-- ============================================================================
-- 019_financeiro.sql — RLS de lancamentos (módulo Financeiro).
--
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration do EF Core InitialCreate do FinanceiroDbContext (cria a tabela e os índices).
-- Mesmo padrão de 018_tarefas_tags.sql: NULLIF(..., '') e WITH CHECK separado no INSERT.
--
-- Lançamentos NÃO são append-only: pagar atualiza a linha e excluir é soft delete (UPDATE).
-- O hard DELETE existe só por consistência com as demais tabelas de negócio.
-- ============================================================================

DO $checks$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_lancamentos_valor_positivo') THEN
    ALTER TABLE lancamentos ADD CONSTRAINT ck_lancamentos_valor_positivo CHECK (valor > 0);
  END IF;

  -- Pago ⇔ tem data de pagamento.
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_lancamentos_pago_em') THEN
    ALTER TABLE lancamentos ADD CONSTRAINT ck_lancamentos_pago_em CHECK ((status = 'Pago') = (pago_em IS NOT NULL));
  END IF;
END
$checks$;

ALTER TABLE lancamentos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_lancamentos ON lancamentos
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_lancamentos_write ON lancamentos
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.lancamentos TO app_user;
