export interface Metric {
  value: number
  /** Variação em relação à semana anterior (unidades absolutas). */
  delta: number
}

export interface DashboardPublication {
  id: string
  source: string
  processNumber: string
  client: string
  excerpt: string
  publishedAt: string
}

export interface DashboardAlert {
  id: string
  title: string
  description: string | null
  type: string
  createdAt: string
}

/**
 * Só o que tem endpoint de verdade. Prazos e agenda ficam de fora: serão cadastrados pelo
 * escritório (Fases 3 e 4), e até lá o painel não mostra número que não existe.
 */
export interface DashboardSummary {
  metrics: {
    activeProcesses: Metric
    unreadPublications: Metric
    pendingReviews: Metric
    unreadAlerts: Metric
  }
  /** As publicações NÃO LIDAS mais recentes. */
  publications: DashboardPublication[]
  /** Os alertas NÃO LIDOS mais recentes. */
  alerts: DashboardAlert[]
}
