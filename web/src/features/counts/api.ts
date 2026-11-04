import type { AxiosInstance } from 'axios'
import { getAlertasSummary } from '@/features/alertas/api'
import { getPublicationsSummary } from '@/features/publicacoes/api'

/** Contadores de pendências exibidos como badge na navegação. */
export interface PendingCounts {
  publications: number
  alerts: number
}

/**
 * Não há um `/me/pending-counts` no backend, e não deve haver: o badge soma pendências de
 * módulos diferentes, e um endpoint único faria o Platform depender deles (ver
 * ModuleBoundaryTests). Cada contador vem do seu módulo e a soma é feita aqui — as duas
 * consultas em paralelo (sem waterfall).
 */
export async function getPendingCounts(client: AxiosInstance): Promise<PendingCounts> {
  const [publications, alerts] = await Promise.all([
    getPublicationsSummary(client),
    getAlertasSummary(client),
  ])

  return {
    // O badge conta o que EXIGE AÇÃO (pendente de revisão), não o que está por ler: uma
    // publicação já lida mas não revisada continua sendo um prazo em aberto.
    publications: publications.untreatedTotal,
    // Já nos alertas, "pendente" é o não lido: o alerta existe para ser visto.
    alerts: alerts.unread,
  }
}
