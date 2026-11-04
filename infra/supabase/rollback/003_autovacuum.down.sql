-- Reverte 003_autovacuum.sql
ALTER TABLE audit_logs RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor);
ALTER TABLE outbox_messages RESET (autovacuum_vacuum_scale_factor, autovacuum_analyze_scale_factor);
