-- ============================================================================
-- 015_atendimento_consulta.sql — RLS e privilégios da sessão de verificação de identidade da
-- consulta de andamento de processo pelo bot (ADR-068), e o índice da busca de cliente por telefone.
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 014_atendimentos.sql
--   - Migration AddConsultaProcesso do AtendimentoDbContext (cria a tabela e os índices B-tree)
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Row-Level Security — mesmo desenho das demais tabelas do módulo (ver 014).
-- ----------------------------------------------------------------------------
ALTER TABLE atendimento_consulta_sessoes ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_atendimento_consulta_sessoes ON atendimento_consulta_sessoes
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_atendimento_consulta_sessoes_write ON atendimento_consulta_sessoes
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- ----------------------------------------------------------------------------
-- Privilégios mínimos. REVOKE primeiro: as default privileges de 002_rls.sql dão CRUD a toda
-- tabela nova (ver 014).
--
-- Sem DELETE: a sessão é prova de quem consultou e quando, e o bloqueio por falhas depende de
-- ela continuar existindo. UPDATE só nas colunas que a consulta altera — nunca tenant_id,
-- atendimento_id, client_id nem started_at, que dizem a quem a sessão pertence.
--
-- A tabela NÃO tem coluna para o documento. O documento digitado nunca é gravado: a comparação
-- acontece em memória e o valor é descartado.
-- ----------------------------------------------------------------------------
REVOKE ALL ON public.atendimento_consulta_sessoes FROM app_user;

GRANT SELECT, INSERT ON public.atendimento_consulta_sessoes TO app_user;
GRANT UPDATE (expires_at, failed_attempts, last_attempt_at, verified_at, blocked_until, process_ids)
  ON public.atendimento_consulta_sessoes TO app_user;

-- ----------------------------------------------------------------------------
-- Busca de cliente pelo telefone do WhatsApp (verificação de identidade).
-- Parcial: só quem tem telefone cadastrado; ativo, pois só cliente ativo é verificado.
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_clientes_tenant_telefone
  ON clientes (tenant_id, telefone)
  WHERE telefone IS NOT NULL AND is_active;
