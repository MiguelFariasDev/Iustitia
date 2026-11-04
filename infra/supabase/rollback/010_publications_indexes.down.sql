-- Reverte 010_publications_indexes.sql
ALTER TABLE publications RESET (
  autovacuum_vacuum_scale_factor,
  autovacuum_analyze_scale_factor
);

DROP INDEX IF EXISTS ix_publications_captured_brin;
DROP INDEX IF EXISTS ix_publications_published_brin;
DROP INDEX IF EXISTS ix_publications_cnj_number_trgm;
DROP INDEX IF EXISTS ix_publications_raw_content_trgm;
DROP INDEX IF EXISTS ix_publications_search;

ALTER TABLE publications DROP COLUMN IF EXISTS search_vector;
