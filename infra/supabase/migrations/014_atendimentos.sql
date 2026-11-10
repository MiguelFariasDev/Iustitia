-- ============================================================================
-- 014_atendimentos.sql — RLS, privilégios e Realtime do módulo Atendimento (inbox + handoff
-- do bot de WhatsApp).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 000_extensions.sql (pg_trgm)
--   - 002_rls.sql (role app_user)
--   - 004_custom_access_token_hook.sql (claims tenant_id e user_role no JWT)
--   - Migration AddAtendimentos do AtendimentoDbContext (cria as tabelas e os índices B-tree)
-- Ver ADR-004 (RLS), ADR-065 (Realtime) e ADR-067 (módulo Atendimento).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Row-Level Security
--
-- NULLIF(..., '') e WITH CHECK separado no INSERT: mesmas razões de 011_publications_rls.sql
-- e 012_clientes_processos.sql.
-- ----------------------------------------------------------------------------
ALTER TABLE atendimentos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_atendimentos ON atendimentos
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_atendimentos_write ON atendimentos
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE atendimento_mensagens ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_atendimento_mensagens ON atendimento_mensagens
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_atendimento_mensagens_write ON atendimento_mensagens
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE atendimento_auditoria ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_atendimento_auditoria ON atendimento_auditoria
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_atendimento_auditoria_write ON atendimento_auditoria
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- ----------------------------------------------------------------------------
-- Privilégios mínimos para o role da aplicação (app_user)
--
-- O REVOKE vem PRIMEIRO e é indispensável: 002_rls.sql configura ALTER DEFAULT PRIVILEGES
-- concedendo SELECT/INSERT/UPDATE/DELETE em toda tabela nova do schema public. Sem revogar, os
-- GRANTs abaixo apenas somariam ao que já existe e nada seria restringido.
--
-- Sem DELETE em nenhuma tabela de conversa: o histórico é prova de atendimento, retido pelo
-- prazo da política (5 anos), e a exclusão por LGPD é um procedimento próprio, nunca um DELETE
-- da aplicação.
-- A trilha de auditoria é IMUTÁVEL: só SELECT e INSERT — nem a aplicação consegue alterá-la.
-- Mensagens aceitam UPDATE apenas nas colunas do resultado do envio (delivery/external_id),
-- gravado depois da criação.
-- ----------------------------------------------------------------------------
REVOKE ALL ON public.atendimentos FROM app_user;
REVOKE ALL ON public.atendimento_mensagens FROM app_user;
REVOKE ALL ON public.atendimento_auditoria FROM app_user;
REVOKE ALL ON public.bot_api_keys FROM app_user;

GRANT SELECT, INSERT ON public.atendimentos TO app_user;
GRANT SELECT, INSERT ON public.atendimento_mensagens TO app_user;
GRANT SELECT, INSERT ON public.atendimento_auditoria TO app_user;

-- UPDATE por COLUNA: o que a aplicação precisa alterar e nada além. O corpo, o remetente e o
-- horário de uma mensagem nunca mudam; o telefone, o escritório e a data de criação de um
-- atendimento também não — assim nem um bug nem uma injeção consegue reescrever o histórico
-- ou reatribuir uma conversa a outro tenant.
GRANT UPDATE (delivery, external_id) ON public.atendimento_mensagens TO app_user;
GRANT UPDATE (client_id, contact_name, status, assigned_user_id, last_message_at, updated_at, updated_by)
  ON public.atendimentos TO app_user;

-- ----------------------------------------------------------------------------
-- bot_api_keys — SEM RLS, de propósito.
--
-- A autenticação do Bot procura a chave pelo hash ANTES de saber o escritório: é a chave que
-- revela o tenant. Com RLS, a consulta sem app.current_tenant devolveria zero linhas e nenhum
-- Bot autenticaria. O acesso é protegido de outro jeito: a coluna guarda só o hash SHA-256 de
-- uma chave de 256 bits (inviável de inverter), e as consultas de gestão (listar/revogar)
-- passam pelo filtro global de tenant do AtendimentoDbContext.
-- ----------------------------------------------------------------------------
GRANT SELECT, INSERT, UPDATE ON public.bot_api_keys TO app_user;

-- ----------------------------------------------------------------------------
-- Busca por nome de contato na fila do Inbox (GIN + pg_trgm) — mesmo desenho de
-- ix_clientes_nome_trgm: quem digita erra, e nome de pessoa não tem stemming útil.
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_atendimentos_contact_name_trgm
  ON atendimentos USING GIN (lower(contact_name) gin_trgm_ops);

-- Busca por trecho do telefone ("final 9999").
CREATE INDEX IF NOT EXISTS ix_atendimentos_phone_trgm
  ON atendimentos USING GIN (phone_number gin_trgm_ops);

-- ----------------------------------------------------------------------------
-- Supabase Realtime — canal privado do escritório: tenant:{tenantId}:atendimentos
--
-- O backend publica pelo Broadcast REST (service role) com "private": true, e o navegador só
-- assina o canal se a policy abaixo permitir. O payload carrega apenas ids e estado (nunca
-- telefone nem texto), mas o canal continua restrito: o tenant sai do JWT validado pelo
-- Supabase, e só quem pode ler atendimentos (Owner/Partner/Lawyer) assina — o mesmo critério
-- da policy LawyerOrAbove dos endpoints.
--
-- O bloco vira no-op em um Postgres puro (testes de integração com Testcontainers).
-- ----------------------------------------------------------------------------
DO $realtime$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'realtime') THEN
    EXECUTE $policy$
      CREATE POLICY atendimentos_realtime_receive ON realtime.messages
        FOR SELECT
        TO authenticated
        USING (
          realtime.messages.extension = 'broadcast'
          AND (SELECT auth.jwt() ->> 'user_role') IN ('Owner', 'Partner', 'Lawyer')
          AND (SELECT realtime.topic()) =
              'tenant:' || (SELECT auth.jwt() ->> 'tenant_id') || ':atendimentos'
        )
    $policy$;
  END IF;
END
$realtime$;
