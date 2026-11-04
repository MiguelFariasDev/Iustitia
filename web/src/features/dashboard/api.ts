import type { AxiosInstance } from 'axios'
import { env } from '@/config/env'
import { endpoints } from '@/lib/api/endpoints'
import {
  toPublication,
  type PublicationsListDto,
  type PublicationsSummaryDto,
} from '@/features/publicacoes/contract'
import type { AlertsPageDto } from '@/features/alertas/contract'
import { mockAlertasSummary, mockListAlertas } from '@/features/alertas/mocks'
import { mockListPublications, mockPublicationsSummary } from '@/features/publicacoes/mocks'
import { mockResponse } from '@/lib/api/mock'
import type { ProcessosPage } from '@/features/processos/types'
import type { DashboardAlert, DashboardPublication, DashboardSummary } from './types'

/** Quantos itens recentes cada lista do painel mostra. */
const RECENT_ITEMS = 5

/**
 * O painel é composto NO CLIENTE a partir dos endpoints de cada módulo — não existe
 * `/dashboard/summary` no backend, e criá-lo obrigaria o Platform a depender dos módulos de
 * negócio (ver ModuleBoundaryTests e o comentário em lib/api/endpoints.ts). As quatro
 * consultas correm em paralelo.
 *
 * Tudo o que aparece aqui vem do banco do escritório. Prazos e agenda NÃO aparecem: não
 * existem no backend (Fases 3 e 4), e um cartão com número inventado é pior que cartão
 * nenhum — quem olha o painel toma decisão com base nele.
 */
export async function getDashboardSummary(
  client: AxiosInstance | undefined
): Promise<DashboardSummary> {
  if (env.useMocks || !client) return mockResponse(buildMockDashboard(), 250)

  const [summaryResponse, publicationsResponse, processosResponse, alertsResponse] =
    await Promise.all([
      client.get<PublicationsSummaryDto>(endpoints.publications.summary),
      client.get<PublicationsListDto>(endpoints.publications.list, {
        params: { page: 1, pageSize: RECENT_ITEMS, isRead: false },
      }),
      // pageSize 1: só interessa o totalCount, não as linhas.
      client.get<ProcessosPage>(endpoints.processos.list, {
        params: { page: 1, pageSize: 1, status: 'Ativo' },
      }),
      // A contagem de não lidos é o totalCount desta mesma consulta: um round-trip só.
      client.get<AlertsPageDto>(endpoints.alerts.list, {
        params: { page: 1, pageSize: RECENT_ITEMS, isRead: false },
      }),
    ])

  const summary = summaryResponse.data

  return {
    metrics: {
      activeProcesses: { value: processosResponse.data.totalCount, delta: 0 },
      unreadPublications: { value: summary.unread, delta: 0 },
      pendingReviews: { value: summary.pendingReview, delta: 0 },
      unreadAlerts: { value: alertsResponse.data.totalCount, delta: 0 },
    },
    publications: publicationsResponse.data.items.map(toDashboardPublication),
    alerts: alertsResponse.data.items.map(toDashboardAlert),
  }
}

function toDashboardPublication(dto: PublicationsListDto['items'][number]): DashboardPublication {
  const publication = toPublication(dto)

  return {
    id: publication.id,
    source: publication.tribunal ?? publication.journal,
    processNumber: publication.processNumber ?? 'Sem processo vinculado',
    client: publication.clienteNome ?? '—',
    excerpt: publication.content,
    publishedAt: publication.publishedAt,
  }
}

function toDashboardAlert(dto: AlertsPageDto['items'][number]): DashboardAlert {
  return {
    id: dto.id,
    title: dto.title,
    description: dto.description,
    type: dto.type,
    createdAt: dto.createdAt,
  }
}

/**
 * Painel com a API simulada (dev e E2E): as mesmas listas das telas de publicações e alertas,
 * para os números do painel baterem com o que as telas mostram ao clicar.
 */
function buildMockDashboard(): DashboardSummary {
  const publications = mockListPublications({ isRead: false, pageSize: RECENT_ITEMS })
  const alerts = mockListAlertas({ isRead: false, pageSize: RECENT_ITEMS })
  const summary = mockPublicationsSummary()

  return {
    metrics: {
      activeProcesses: { value: 12, delta: 0 },
      unreadPublications: { value: summary.unread, delta: 0 },
      pendingReviews: { value: summary.untreatedTotal, delta: 0 },
      unreadAlerts: { value: mockAlertasSummary().unread, delta: 0 },
    },
    publications: publications.items.map((item) => ({
      id: item.id,
      source: item.tribunal ?? item.journal,
      processNumber: item.processNumber ?? 'Sem processo vinculado',
      client: item.clienteNome ?? '—',
      excerpt: item.content,
      publishedAt: item.publishedAt,
    })),
    alerts: alerts.items.map((alert) => ({
      id: alert.id,
      title: alert.title,
      description: alert.description,
      type: alert.type,
      createdAt: alert.createdAt,
    })),
  }
}
