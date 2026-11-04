-- ============================================================================
-- 012_clientes_processos.sql — RLS e índices avançados de clientes e processos.
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 000_extensions.sql (pg_trgm)
--   - 002_rls.sql (role app_user)
--   - Migration AddClientesEProcessos do LegalDbContext
-- Ver ADR-004 (RLS) e ADR-057 (estratégia de índices).
--
-- Os índices B-tree e parciais destas tabelas vivem nas configurações Fluent API e são
-- criados pela migration do EF Core — não redeclarar aqui. Este arquivo cobre só o que o
-- EF não expressa (GIN/trigram) e o RLS.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Row-Level Security
--
-- NULLIF(..., '') é essencial: o Npgsql reutiliza conexões do pool e o DISCARD ALL reseta
-- um GUC customizado para STRING VAZIA, não para ausente. Sem o NULLIF, ''::uuid levanta
-- 22P02 na primeira query de uma conexão reciclada sem tenant — continua sendo fail closed
-- (nada vaza), mas quebra a requisição. Ver 011_publications_rls.sql.
--
-- WITH CHECK separado no INSERT: a policy de USING restringe apenas a LEITURA. Sem ele,
-- uma escrita com tenant_id de outro escritório passaria.
-- ----------------------------------------------------------------------------

ALTER TABLE clientes ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_clientes ON clientes
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_clientes_write ON clientes
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

ALTER TABLE processos ENABLE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_processos ON processos
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

CREATE POLICY tenant_isolation_processos_write ON processos
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON public.clientes TO app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON public.processos TO app_user;

-- ----------------------------------------------------------------------------
-- Busca fuzzy por nome de cliente (GIN + pg_trgm).
--
-- Sobre nome_busca (a coluna derivada em minúsculas), não sobre `nome`: `nome` é um value
-- object mapeado por HasConversion e a aplicação nunca o consulta por texto — ver
-- Cliente.NomeBusca.
--
-- Trigram e não full-text: nome de pessoa não tem stemming útil ("Gonçalves" não deriva de
-- nada) e quem digita erra — "silva" precisa achar "Sillva".
-- ----------------------------------------------------------------------------
CREATE INDEX IF NOT EXISTS ix_clientes_nome_trgm
  ON clientes USING GIN (nome_busca gin_trgm_ops);

-- Busca por número de processo parcial ("0000777" encontra 0000777-07.2018.8.06.0100).
CREATE INDEX IF NOT EXISTS ix_processos_cnj_digits_trgm
  ON processos USING GIN (cnj_digits gin_trgm_ops);

-- Busca por parte contrária (coluna de texto simples, sem conversor).
CREATE INDEX IF NOT EXISTS ix_processos_parte_contraria_trgm
  ON processos USING GIN (lower(parte_contraria) gin_trgm_ops);
