import type { AxiosInstance } from 'axios'
import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { ApiError } from '@/lib/api/errors'
import { endpoints } from '@/lib/api/endpoints'
import { mockResponse } from '@/lib/api/mock'
import {
  statusToApiFilter,
  toPublication,
  toPublicationDetail,
  toPublicationsSummary,
  type PublicationApiStatus,
  type PublicationDetailDto,
  type PublicationsListDto,
  type PublicationsSummaryDto,
} from './contract'
import {
  mockChangeStatus,
  mockGetPublication,
  mockListPublications,
  mockMarkAsRead,
  mockPublicationsSummary,
} from './mocks'
import type {
  Publication,
  PublicationsFilters,
  PublicationsPage,
  PublicationsSummary,
} from './types'

export const DEFAULT_PAGE_SIZE = 20

/** Recebe o cliente HTTP (navegador ou servidor) — a mesma função serve aos dois lados. */
export async function getPublications(
  filters: PublicationsFilters = {},
  client: AxiosInstance | undefined = apiClient
): Promise<PublicationsPage> {
  const normalized = { ...filters, pageSize: filters.pageSize ?? DEFAULT_PAGE_SIZE }
  if (env.useMocks || !client) return mockResponse(mockListPublications(normalized), 300)

  const { data } = await client.get<PublicationsListDto>(endpoints.publications.list, {
    params: {
      page: normalized.page ?? 1,
      pageSize: normalized.pageSize,
      status: normalized.status ? statusToApiFilter[normalized.status] : undefined,
      tribunal: normalized.tribunal || undefined,
      clienteId: normalized.clienteId || undefined,
      processId: normalized.processId || undefined,
      dataInicio: normalized.from || undefined,
      dataFim: normalized.to || undefined,
      isRead: normalized.isRead ?? undefined,
      busca: normalized.busca || undefined,
      ordem: normalized.ordem && normalized.ordem !== 'recent' ? normalized.ordem : undefined,
    },
  })

  return {
    items: data.items.map((item) => toPublication(item)),
    page: data.page,
    pageSize: data.pageSize,
    totalCount: data.totalCount,
    totalPages: data.totalPages,
  }
}

export async function getPublicationsSummary(
  client: AxiosInstance | undefined = apiClient
): Promise<PublicationsSummary> {
  if (env.useMocks || !client) return mockResponse(mockPublicationsSummary(), 150)

  const { data } = await client.get<PublicationsSummaryDto>(endpoints.publications.summary)

  return toPublicationsSummary(data)
}

export async function getPublicationById(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<Publication> {
  if (env.useMocks || !client) {
    const found = mockGetPublication(id)
    if (!found)
      throw new ApiError(404, {
        errorCode: 'PUBLICATION_NOT_FOUND',
        detail: 'Publicação não encontrada.',
      })
    return mockResponse(found, 200)
  }

  const { data } = await client.get<PublicationDetailDto>(endpoints.publications.byId(id))

  return toPublicationDetail(data)
}

/**
 * Marca como lida. NÃO é aprovação: o status de revisão só muda por ação explícita do
 * advogado (ver ADR-012) — endpoint próprio, `changePublicationStatus`.
 */
export async function markPublicationAsRead(id: string): Promise<void> {
  if (env.useMocks) {
    const result = mockMarkAsRead(id)
    if (result === 'not-found') throw new ApiError(404, { errorCode: 'PUBLICATION_NOT_FOUND' })
    if (result === 'already-read')
      throw new ApiError(409, { errorCode: 'PUBLICATION_ALREADY_READ' })
    return mockResponse(undefined, 100)
  }

  await apiClient.patch(endpoints.publications.read(id))
}

/** Revisão humana da publicação (PendingReview → Approved/Corrected/Rejected/Archived). */
export async function changePublicationStatus(
  id: string,
  status: PublicationApiStatus,
  notes?: string
): Promise<void> {
  if (env.useMocks) {
    const result = mockChangeStatus(id, status, notes)
    if (result === 'not-found') throw new ApiError(404, { errorCode: 'PUBLICATION_NOT_FOUND' })
    if (result === 'invalid-transition') {
      throw new ApiError(409, { errorCode: 'PUBLICATION_INVALID_STATUS_TRANSITION' })
    }
    return mockResponse(undefined, 150)
  }

  await apiClient.patch(endpoints.publications.status(id), { status, notes })
}

/** Publicações capturadas para um processo (a tela do processo mostra as mais recentes). */
export async function getPublicationsByProcess(processId: string): Promise<Publication[]> {
  const page = await getPublications({ processId, pageSize: 20 })

  return page.items
}
