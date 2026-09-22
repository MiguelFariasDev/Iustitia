-- ============================================================================
-- 000_extensions.sql — Extensões do PostgreSQL/Supabase
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- ============================================================================
-- pg_trgm: busca fuzzy (usada pelo índice trigram em users.name — ver 001_fulltext.sql)
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- pgcrypto: criptografia em nível de coluna (dados sensíveis — ver ADR-004/LGPD)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- pg_stat_statements: monitoramento de queries (ver docs/database/performance.md)
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;

-- uuid-ossp: geração de UUID no banco (a aplicação já gera UUIDs em C#, mas a extensão
-- fica disponível para uso em scripts/seeds/consultas administrativas)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
