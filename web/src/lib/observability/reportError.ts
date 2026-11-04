/**
 * Ponto único de reporte de erros do frontend. Hoje registra no console (permitido pela
 * regra de lint: apenas warn/error); na fase de observabilidade web, enviará ao
 * Application Insights junto do X-Correlation-Id.
 */
export function reportError(error: unknown, context?: Record<string, unknown>) {
  if (process.env.NODE_ENV === 'test') return
  console.error('[app:error]', error, context)
}
