-- Reverte 019_financeiro.sql
REVOKE SELECT, INSERT, UPDATE, DELETE ON public.lancamentos FROM app_user;

DROP POLICY IF EXISTS tenant_isolation_lancamentos_write ON lancamentos;
DROP POLICY IF EXISTS tenant_isolation_lancamentos ON lancamentos;
ALTER TABLE lancamentos DISABLE ROW LEVEL SECURITY;

ALTER TABLE lancamentos DROP CONSTRAINT IF EXISTS ck_lancamentos_pago_em;
ALTER TABLE lancamentos DROP CONSTRAINT IF EXISTS ck_lancamentos_valor_positivo;
