-- ============================================================================
-- 015_ai_usage_logs.sql — RLS, constraints e grants de ai_usage_logs (Fase 3, Etapa 3.1 —
-- auditoria de uso de IA do módulo Integrations.Anthropic).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration InitialCreate do AnthropicDbContext, que CRIA a tabela e seus índices
--     (ix_ai_usage_logs_tenant_created, ix_ai_usage_logs_reference).
-- Ver ADR-004 (RLS) e ADR-069 (auditoria e privacidade na IA).
--
-- Mesma convenção de 011–014: a tabela é criada pelo EF Core; este arquivo cobre só o que o EF
-- não expressa (RLS, CHECK constraints, grants). Criar a tabela aqui também faria a migration do
-- EF falhar com "relation already exists".
--
-- A tabela NÃO tem coluna para texto de prompt nem de resposta, e não deve ganhar: o sigilo
-- profissional (OAB/LGPD) impede registrar o conteúdo trocado com a IA.
-- ============================================================================

-- CHECK constraints: defesa em profundidade sobre o código. Idempotente para reaplicar em dev.
DO $checks$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ai_usage_logs_non_negative') THEN
    ALTER TABLE ai_usage_logs
      ADD CONSTRAINT ck_ai_usage_logs_non_negative
      CHECK (input_tokens >= 0 AND output_tokens >= 0 AND cache_creation_input_tokens >= 0
             AND cache_read_input_tokens >= 0 AND cost_usd >= 0 AND latency_ms >= 0);
  END IF;

  -- Sucesso e código de erro são mutuamente exclusivos.
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_ai_usage_logs_error_code') THEN
    ALTER TABLE ai_usage_logs
      ADD CONSTRAINT ck_ai_usage_logs_error_code CHECK (success = (error_code IS NULL));
  END IF;
END
$checks$;

-- NULLIF(..., '') e WITH CHECK separado: mesmas razões de 011_publications_rls.sql. Os jobs
-- gravam em nome de vários escritórios em sequência, sem usuário autenticado.
ALTER TABLE ai_usage_logs ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_ai_usage_logs ON ai_usage_logs
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_ai_usage_logs_write ON ai_usage_logs
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- Trilha de auditoria APPEND-ONLY: a aplicação só lê e insere. Retenção/expurgo, quando
-- existir, será um job com papel próprio.
GRANT SELECT, INSERT ON public.ai_usage_logs TO app_user;
