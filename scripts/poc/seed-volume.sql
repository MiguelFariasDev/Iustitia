-- ============================================================================
-- seed-volume.sql — Gera publicações SINTÉTICAS para medir o comportamento dos
-- índices em escala (Etapa 1.3, Parte 6.3).
--
-- Necessário porque o planner do Postgres escolhe seq scan corretamente numa
-- tabela de 43 linhas: sem volume, um EXPLAIN ANALYZE não diz NADA sobre se a
-- estratégia de índices da ADR-057 funciona.
--
-- Tenant dedicado (33333333-...) para não contaminar os dados reais da PoC.
-- Remover depois com: DELETE FROM publications WHERE tenant_id = '33333333-3333-4333-8333-333333333333';
-- ============================================================================

INSERT INTO tenants (id, name, cnpj, status, created_at, is_deleted)
VALUES ('33333333-3333-4333-8333-333333333333', 'Escritorio Carga Sintetica', '11555222000133', 'Active', now(), false)
ON CONFLICT (id) DO NOTHING;

INSERT INTO publications (
  id, tenant_id, process_id, cnj_number, external_id, source, raw_content,
  published_at, captured_at, type, status, is_read, metadata_json,
  created_at, is_deleted)
SELECT
  gen_random_uuid(),
  '33333333-3333-4333-8333-333333333333',
  -- ~200 processos distintos, para a timeline por processo ter seletividade realista
  ('00000000-0000-4000-8000-' || lpad((i % 200)::text, 12, '0'))::uuid,
  lpad((1000000 + i)::text, 7, '0') || '-00.2026.8.26.0001',
  'sint-' || i,
  'DJEN',
  'Publicacao sintetica ' || i || '. Intimacao para manifestacao no prazo legal de 15 dias uteis. '
    || repeat('Texto de preenchimento para aproximar o tamanho medio real de uma comunicacao. ', 12),
  now() - ((i % 720) || ' days')::interval,
  now(),
  NULL,
  -- ~5% pendentes de revisão: proporção realista em regime estacionário, que é o
  -- que torna o índice PARCIAL de fila de revisão pequeno e eficaz (ADR-057)
  CASE WHEN i % 20 = 0 THEN 'PendingReview' ELSE 'Approved' END,
  (i % 10 <> 0),
  '{"sintetico": true}'::jsonb,
  now(),
  false
FROM generate_series(1, 50000) AS i;

ANALYZE publications;
SELECT count(*) AS total_publications FROM publications;
