-- ============================================================================
-- 003_autovacuum.sql — Tuning de autovacuum para tabelas de alto volume de escrita
-- Aplicar via: npx supabase db push (ou psql direto em dev local)
-- ============================================================================
-- audit_logs e outbox_messages só crescem por INSERT (a primeira nunca é
-- atualizada; a segunda é atualizada uma única vez ao ser processada) — um
-- autovacuum mais agressivo evita bloat e mantém as estatísticas do planner
-- atualizadas com mais frequência do que o padrão (scale_factor 0.2/0.1).

ALTER TABLE audit_logs SET (
  autovacuum_vacuum_scale_factor = 0.05,
  autovacuum_analyze_scale_factor = 0.02
);

ALTER TABLE outbox_messages SET (
  autovacuum_vacuum_scale_factor = 0.05,
  autovacuum_analyze_scale_factor = 0.02
);
