-- ============================================================================
-- 021_movimentacoes_notificacoes.sql — RLS e regras de processo_movimentacoes e
-- notificacoes_cliente (acompanhamento de movimentações, módulo Legal — ADR-075).
--
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration do EF Core AddMovimentacoesENotificacoesCliente (LegalDbContext).
-- Mesmo padrão de 018_tarefas_tags.sql: NULLIF(..., '') e WITH CHECK separado no INSERT.
-- Numerada 021 (e não 020) para não colidir com a 020 de outra branch em andamento.
-- ============================================================================

-- Andamentos capturados: conteúdo de processo, isolado por escritório.
ALTER TABLE processo_movimentacoes ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_processo_movimentacoes ON processo_movimentacoes
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_processo_movimentacoes_write ON processo_movimentacoes
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- Avisos ao cliente: rascunho do job, enviados só depois da revisão do advogado.
ALTER TABLE notificacoes_cliente ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_notificacoes_cliente ON notificacoes_cliente
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_notificacoes_cliente_write ON notificacoes_cliente
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

DO $checks$
BEGIN
  -- Revisão humana obrigatória, também no banco: nenhum aviso Enviada sem revisor e data de envio.
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_notificacoes_cliente_enviada_revisada') THEN
    ALTER TABLE notificacoes_cliente ADD CONSTRAINT ck_notificacoes_cliente_enviada_revisada
      CHECK (status <> 'Enviada' OR (revisado_por IS NOT NULL AND revisado_em IS NOT NULL AND enviado_em IS NOT NULL));
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_notificacoes_cliente_status') THEN
    ALTER TABLE notificacoes_cliente ADD CONSTRAINT ck_notificacoes_cliente_status
      CHECK (status IN ('PendenteRevisao', 'Enviada', 'Descartada', 'FalhaEnvio'));
  END IF;
END
$checks$;

-- O REVOKE vem primeiro: 002_rls.sql dá UPDATE/DELETE por padrão a app_user, e um GRANT posterior
-- não retira o que já foi concedido (mesmo motivo de 015_ai_usage_logs.sql).
-- Andamento é imutável: a aplicação só lê e insere.
REVOKE ALL ON public.processo_movimentacoes FROM app_user;
GRANT SELECT, INSERT ON public.processo_movimentacoes TO app_user;

-- Aviso muda de status (enviado, descartado), mas não é apagado: é o registro do que foi comunicado.
REVOKE ALL ON public.notificacoes_cliente FROM app_user;
GRANT SELECT, INSERT, UPDATE ON public.notificacoes_cliente TO app_user;
