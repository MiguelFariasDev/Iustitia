# ADR-026: Estratégia de Índices e Performance

**Status:** Aceito — implementado na Etapa 0.2

## Contexto

O Iustitia é um sistema jurídico com padrões de consulta previsíveis e
frequentes: busca por número CNJ, listagem de processos/tarefas por
responsável e status, auditoria por tenant e período, busca textual por
nome de cliente/publicação. Alguns desses padrões (busca fuzzy, full-text,
séries temporais monotônicas) não têm bom suporte nativo no Fluent API do
EF Core, mas são bem resolvidos por recursos específicos do PostgreSQL.

## Decisão

Dividir a estratégia de índices em duas camadas, com dois pontos de
verdade complementares:

1. **Índices comuns via Fluent API do EF Core** (versionados como
   migrations do EF Core, em `Platform.Infrastructure/Persistence/Migrations`):
   chave primária, unique, compostos e parciais — tudo que o EF Core sabe
   expressar nativamente. Ex.: `ux_tenants_cnpj` (unique),
   `ix_users_tenant_role` (composto), `ix_users_tenant_active_only`
   (parcial, `HasFilter("is_deleted = false")`).

2. **Índices avançados via SQL raw**, como migrations do Supabase
   (`infra/supabase/migrations/001_fulltext.sql`), aplicadas via
   `npx supabase db push`, fora do controle de migrations do EF Core:
   - **GIN + tsvector** para full-text search em português
     (`users.search_vector`, coluna gerada `GENERATED ALWAYS AS
     to_tsvector('portuguese', name) STORED`).
   - **GIN + pg_trgm** para busca fuzzy tolerante a erro de digitação
     (`ix_users_name_trgm`).
   - **BRIN** para colunas que só crescem no tempo e nunca são atualizadas
     (`audit_logs.created_at`) — muito mais compacto que B-tree em tabelas
     grandes.

RLS, roles e extensões também vivem nas migrations do Supabase (ver
ADR-004), pelo mesmo motivo: são recursos específicos do Postgres que o
EF Core não expressa nativamente.

Lista completa de índices, quando usar cada tipo e como monitorar:
`docs/database/indexes.md`.

## Consequências

**Positivas:**
- Cada índice usa a ferramenta certa: Fluent API onde é suficiente e
  versionado junto com o modelo C#; SQL raw onde é necessário, versionado
  junto com o resto da infraestrutura de banco.
- Todos os índices são **verificados por teste de integração real** contra
  Postgres (`IndexTests`, Etapa 0.2) — não é só documentação, é testado.

**Negativas / trade-offs:**
- Dois pontos de verdade para o schema (migrations do EF Core + migrations
  SQL do Supabase) exigem disciplina: mudar uma tabela com índice avançado
  precisa lembrar de rodar as duas ferramentas, na ordem certa (EF Core
  primeiro, para criar a tabela; Supabase depois, para os recursos
  avançados que dependem dela existir).
- Colunas geradas (`search_vector`) não são conhecidas pelo modelo do EF
  Core — isso é intencional (EF Core não teria como expressar `tsvector`
  de forma útil), mas significa que o modelo C# e o schema real do banco
  não são 100% idênticos; isso deve ficar documentado e óbvio para quem
  mexer no schema depois.
