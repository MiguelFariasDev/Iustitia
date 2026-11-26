-- Reverte 021_movimentacoes_notificacoes.sql
REVOKE SELECT, INSERT, UPDATE ON public.notificacoes_cliente FROM app_user;
REVOKE SELECT, INSERT ON public.processo_movimentacoes FROM app_user;

ALTER TABLE notificacoes_cliente DROP CONSTRAINT IF EXISTS ck_notificacoes_cliente_status;
ALTER TABLE notificacoes_cliente DROP CONSTRAINT IF EXISTS ck_notificacoes_cliente_enviada_revisada;

DROP POLICY IF EXISTS tenant_isolation_notificacoes_cliente_write ON notificacoes_cliente;
DROP POLICY IF EXISTS tenant_isolation_notificacoes_cliente ON notificacoes_cliente;
ALTER TABLE notificacoes_cliente DISABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation_processo_movimentacoes_write ON processo_movimentacoes;
DROP POLICY IF EXISTS tenant_isolation_processo_movimentacoes ON processo_movimentacoes;
ALTER TABLE processo_movimentacoes DISABLE ROW LEVEL SECURITY;
