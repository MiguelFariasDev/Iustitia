-- ============================================================================
-- poc-tenant.sql — Tenant e alvos da prova de conceito da captura do CNJ
-- (Etapa 1.3). Ver docs/poc/cnj-poc-report.md.
--
-- Uso: docker exec -i advocacia-postgres psql -U postgres -d advocacia_db < scripts/seed/poc-tenant.sql
--
-- POR QUE INSERE O TENANT DIRETO NO BANCO, sem passar pela API:
-- o cadastro self-service (POST /api/v1/tenants) provisiona o administrador no
-- Supabase Auth, e o Supabase não está configurado neste ambiente. A PoC valida
-- a integração com o CNJ, não o fluxo de cadastro — então o tenant é semeado e o
-- fluxo de auth fica fora do escopo desta etapa.
--
-- POR QUE NÃO HÁ TABELA `processes`:
-- o agregado Process é da Fase 2. Os processos de interesse da PoC vivem em
-- Jobs:CnjCapture:CnjNumbers (appsettings), não no banco. A partir da Fase 2 a
-- lista passa a vir do cadastro de processos de cada tenant.
--
-- PROCESSOS UTILIZADOS: todos são números CNJ reais, obtidos do DJEN, que é um
-- diário público. NENHUM é processo de cliente do escritório — não há dado
-- pessoal sob sigilo profissional sendo usado sem autorização (ver claude.md,
-- seção 14, e a Parte 12 do prompt da Etapa 1.3).
-- ============================================================================

-- Tenant A: alvo da captura.
INSERT INTO tenants (id, name, cnpj, status, created_at, is_deleted)
VALUES ('11111111-1111-4111-8111-111111111111', 'Escritorio Piloto', '11222333000181', 'Active', now(), false)
ON CONFLICT (id) DO NOTHING;

-- Tenant B: existe só para provar o isolamento por RLS (nunca recebe captura).
INSERT INTO tenants (id, name, cnpj, status, created_at, is_deleted)
VALUES ('22222222-2222-4222-8222-222222222222', 'Escritorio Controle RLS', '11444777000161', 'Active', now(), false)
ON CONFLICT (id) DO NOTHING;

SELECT id, name, status FROM tenants
WHERE id IN ('11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222');
