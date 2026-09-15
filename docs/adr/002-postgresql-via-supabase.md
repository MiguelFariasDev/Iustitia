# ADR-002: PostgreSQL via Supabase como banco principal

**Status:** Aceito

## Contexto

O sistema precisa de um banco relacional robusto, com suporte a Row-Level
Security (essencial para isolamento multi-tenant — ver ADR-004), JSONB para
payloads semi-estruturados (respostas de IA, endereços, configurações),
full-text search e busca fuzzy (para localizar processos/clientes). A equipe
também quer minimizar o tempo gasto operando infraestrutura de banco de
dados (backups, patching, failover) para focar no domínio de negócio.

Alternativas consideradas: PostgreSQL auto-gerenciado no Azure (Azure
Database for PostgreSQL), SQL Server (Azure SQL), e Supabase.

## Decisão

Usar **Supabase** como provedor gerenciado de PostgreSQL 16+, aproveitando:
- Row-Level Security nativa do Postgres, orquestrada pelo Supabase Auth.
- Extensões prontas: `pgcrypto` (criptografia em coluna), `pg_trgm` (busca
  fuzzy), `uuid-ossp`.
- Supabase Realtime (websockets) para atualizações ao vivo no frontend React.
- Supabase Storage para documentos anexados.
- Backups automáticos gerenciados.

O backend .NET acessa o mesmo banco via EF Core/Dapper, tratando o Supabase
como "apenas Postgres" para fins de acesso a dados — a lógica de negócio
nunca depende de recursos proprietários do Supabase além de Auth/Realtime/
Storage, que têm suas próprias ADRs (003, 018).

## Consequências

**Positivas:**
- Menor esforço operacional (sem gerenciar patching/backup manualmente).
- RLS nativa do Postgres reforça o isolamento multi-tenant na camada de dados,
  não apenas na aplicação.
- Realtime e Storage já integrados, sem precisar orquestrar serviços extras.

**Negativas / trade-offs:**
- Acoplamento a um provedor terceiro para banco crítico (mitigado: é
  PostgreSQL puro por baixo, migração para outro Postgres gerenciado é
  viável se necessário).
- Latência de rede entre Azure (backend) e Supabase (banco) — a decisão de
  região/hospedagem está em `docs/requisitos/15-decisoes-pendentes.md`.
