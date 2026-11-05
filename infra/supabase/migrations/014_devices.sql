-- ============================================================================
-- 014_devices.sql — RLS da tabela devices (dispositivos móveis e push).
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (role app_user)
--   - Migration AddDevices do PlatformDbContext (cria a tabela e os índices
--     ux_devices_tenant_token e ix_devices_tenant_user_revoked)
-- Ver ADR-004 (RLS) e ADR-069 (dispositivos, push e revogação).
--
-- A policy isola por TENANT. O isolamento por USUÁRIO (cada um vê e revoga só os seus
-- dispositivos) é feito pela aplicação — DeviceRepository filtra tenant E usuário —, porque a
-- API conecta como app_user e não como o usuário final: o banco só conhece app.current_tenant.
-- É a mesma decisão da tabela alerts (013) e tem uma razão extra aqui: o registro precisa
-- achar, por token, a linha de um colega do mesmo escritório quando um aparelho compartilhado
-- troca de dono; uma policy por usuário impediria essa leitura e geraria linhas duplicadas.
--
-- device_token é o endereço de entrega do push: nenhuma consulta o devolve à API.
-- ============================================================================

ALTER TABLE devices ENABLE ROW LEVEL SECURITY;

-- NULLIF(..., '') e WITH CHECK separado: mesmas razões de 011_publications_rls.sql (conexão
-- reciclada do pool devolve '' e não NULL; sem WITH CHECK a escrita de outro tenant passaria).
CREATE POLICY tenant_isolation_devices ON devices
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_devices_write ON devices
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.devices TO app_user;
