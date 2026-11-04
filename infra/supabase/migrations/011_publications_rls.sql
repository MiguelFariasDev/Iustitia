-- ============================================================================
-- 011_publications_rls.sql — Row-Level Security da tabela publications.
--
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- Pré-requisitos:
--   - 002_rls.sql (cria a role app_user e o padrão de policy por tenant)
--   - Migration InitialCreate do LegalDbContext (cria a tabela publications)
-- Ver ADR-004 (Row-Level Security para multi-tenancy).
--
-- Publicações são o dado mais sensível capturado pelo sistema: o inteiro teor de
-- comunicações processuais, protegido por sigilo profissional. O isolamento por
-- tenant aqui é a camada definitiva — o filtro global do LegalDbContext é apenas
-- defesa em profundidade e pode ser contornado por IgnoreQueryFilters().
-- ============================================================================

ALTER TABLE publications ENABLE ROW LEVEL SECURITY;

-- current_setting(..., true) com missing_ok = true: sem app.current_tenant definido na
-- sessão (via TenantContextInterceptor), retorna NULL, e comparação com NULL nunca é
-- verdadeira — a policy nega por padrão (fail closed) em vez de vazar dados por
-- esquecimento de configurar o tenant.
--
-- NULLIF(..., '') é essencial e é a diferença em relação ao padrão de 002_rls.sql: o
-- Npgsql reutiliza conexões físicas do pool e, ao devolver uma conexão, o DISCARD ALL
-- reseta um GUC customizado para STRING VAZIA, não para ausente. Sem o NULLIF,
-- ''::uuid levanta "22P02: invalid input syntax for type uuid" na primeira query de uma
-- conexão reciclada sem tenant — um erro bruto de banco em vez do fail closed
-- pretendido. Continua sendo seguro (nada vaza), mas quebra a requisição.
CREATE POLICY tenant_isolation_publications ON publications
  USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- WITH CHECK explícito no INSERT/UPDATE: sem ele, a policy acima restringe apenas a
-- LEITURA, e uma escrita com tenant_id de outro escritório passaria. O job de captura
-- roda sem usuário autenticado e grava em nome de vários tenants em sequência — é
-- exatamente o caminho de código onde um tenant_id errado seria mais fácil de
-- introduzir e mais difícil de notar.
CREATE POLICY tenant_isolation_publications_write ON publications
  FOR INSERT
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);

-- app_user (a role da aplicação em runtime) já recebeu GRANT em ALL TABLES por
-- 002_rls.sql, mas aquele GRANT alcançou só as tabelas que existiam naquele momento.
-- O ALTER DEFAULT PRIVILEGES de lá cobre tabelas novas criadas pelo MESMO papel que
-- o executou; repetir o grant aqui explicitamente torna a migration independente
-- dessa condição.
GRANT SELECT, INSERT, UPDATE, DELETE ON public.publications TO app_user;
