-- ============================================================================
-- 014_publication_suggestions.sql — RLS, constraints e grants de publication_suggestions
-- (Fase 3, Etapa 3.2 — classificação de publicações por IA).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration AddPublicationSuggestions do LegalDbContext, que CRIA a tabela e seus índices
--     (ux_publication_suggestions_publication único em publication_id,
--     ix_publication_suggestions_created_at).
-- A auditoria de uso de IA (ai_usage_logs) é do módulo Anthropic: ver 015_ai_usage_logs.sql.
-- Ver ADR-004 (RLS) e ADR-070 (classificação por IA).
--
-- Seguindo a convenção dos arquivos 011/012/013, a tabela é criada pelo EF Core e este arquivo
-- cobre só o que o EF não expressa: RLS, CHECK constraints e grants. Criar a tabela aqui E na
-- migration do EF faria a segunda a aplicar falhar com "relation already exists".
-- ============================================================================

-- ----------------------------------------------------------------------------
-- CHECK constraints — defesa em profundidade sobre o validador da aplicação
-- (AiResponseParser): o banco também recusa um valor fora do contrato, mesmo que um bug
-- futuro deixe passar. Idempotente para poder reaplicar em dev.
-- ----------------------------------------------------------------------------
DO $checks$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_publication_suggestions_confidence') THEN
    ALTER TABLE publication_suggestions
      ADD CONSTRAINT ck_publication_suggestions_confidence CHECK (confidence BETWEEN 0 AND 100);
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_publication_suggestions_deadline_type') THEN
    ALTER TABLE publication_suggestions
      ADD CONSTRAINT ck_publication_suggestions_deadline_type
      CHECK (deadline_type IS NULL OR deadline_type IN ('business', 'calendar'));
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_publication_suggestions_deadline_pair') THEN
    -- Prazo e forma de contagem andam juntos: um sem o outro não é sugestão utilizável.
    ALTER TABLE publication_suggestions
      ADD CONSTRAINT ck_publication_suggestions_deadline_pair
      CHECK ((suggested_deadline_days IS NULL) = (deadline_type IS NULL));
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_publication_suggestions_deadline_days') THEN
    ALTER TABLE publication_suggestions
      ADD CONSTRAINT ck_publication_suggestions_deadline_days
      CHECK (suggested_deadline_days IS NULL OR suggested_deadline_days BETWEEN 0 AND 365);
  END IF;
END
$checks$;

-- ----------------------------------------------------------------------------
-- Row-Level Security
--
-- NULLIF(..., '') e WITH CHECK separado: mesmas razões de 011_publications_rls.sql. A
-- sugestão e o log de uso contêm metadados derivados de comunicações processuais sigilosas;
-- o job roda sem usuário autenticado e grava em nome de vários escritórios em sequência.
-- ----------------------------------------------------------------------------
ALTER TABLE publication_suggestions ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_publication_suggestions ON publication_suggestions
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_publication_suggestions_write ON publication_suggestions
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- A revisão humana (Etapa 3.3) vai atualizar a sugestão, e a exclusão física atende o direito
-- ao esquecimento (LGPD).
GRANT SELECT, INSERT, UPDATE, DELETE ON public.publication_suggestions TO app_user;
