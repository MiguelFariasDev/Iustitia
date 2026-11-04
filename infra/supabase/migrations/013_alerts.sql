-- ============================================================================
-- 013_alerts.sql — RLS da tabela alerts e autorização do canal Realtime de alertas.
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - 004_custom_access_token_hook.sql (claim tenant_id no JWT)
--   - Migration AddAlerts do PlatformDbContext (cria a tabela e os índices
--     ix_alerts_tenant_user_read, ix_alerts_tenant_type_created, ix_alerts_reference)
-- Ver ADR-004 (RLS) e ADR-065 (alertas in-app).
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Row-Level Security da tabela
--
-- NULLIF(..., '') e WITH CHECK separado: mesmas razões de 011_publications_rls.sql.
-- A policy isola por TENANT; o isolamento por USUÁRIO (cada um vê só os seus alertas) é
-- feito na aplicação (AlertRepository filtra tenant E usuário), porque a aplicação conecta
-- como app_user e não como o usuário final.
-- ----------------------------------------------------------------------------
ALTER TABLE alerts ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_alerts ON alerts
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_alerts_write ON alerts
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.alerts TO app_user;

-- ----------------------------------------------------------------------------
-- Supabase Realtime — canal privado por usuário: tenant:{tenantId}:alerts:{userId}
--
-- O backend publica pelo Broadcast REST (service role) com "private": true, e o navegador
-- só consegue assinar o canal se a policy abaixo permitir. Sem ela, qualquer cliente com a
-- chave anon que soubesse (ou adivinhasse) os ids poderia escutar os alertas de outro usuário
-- — e o texto do alerta carrega o número do processo.
--
-- O topic é montado só com claims do JWT já validado pelo Supabase (tenant_id vem do
-- Custom Access Token Hook, 004; sub = auth.uid()), então não há como assinar o canal de
-- outro tenant nem de outro colega do mesmo escritório.
-- ----------------------------------------------------------------------------
-- O schema "realtime" e as funções auth.* só existem num projeto Supabase de verdade; o
-- bloco abaixo vira no-op em um Postgres puro (testes de integração com Testcontainers).
DO $realtime$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'realtime') THEN
    EXECUTE $policy$
      CREATE POLICY alerts_realtime_receive ON realtime.messages
        FOR SELECT
        TO authenticated
        USING (
          realtime.messages.extension = 'broadcast'
          AND (SELECT realtime.topic()) =
              'tenant:' || (SELECT auth.jwt() ->> 'tenant_id') || ':alerts:' || (SELECT auth.uid())::text
        )
    $policy$;
  END IF;
END
$realtime$;
