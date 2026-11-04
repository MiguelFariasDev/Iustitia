-- Reverte 001_fulltext.sql
DROP INDEX IF EXISTS ix_audit_logs_created_brin;
DROP INDEX IF EXISTS ix_users_name_trgm;
DROP INDEX IF EXISTS ix_users_search;
ALTER TABLE users DROP COLUMN IF EXISTS search_vector;
