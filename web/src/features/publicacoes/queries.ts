import { queryOptions } from '@tanstack/react-query'
import type { AxiosInstance } from 'axios'
import { queryKeys } from '@/lib/query/queryKeys'
import {
  DEFAULT_PAGE_SIZE,
  getPublicationById,
  getPublications,
  getPublicationsSummary,
} from './api'
import type { PublicationsFilters } from './types'

/** Normaliza para a chave não mudar por diferença irrelevante (undefined vs null vs ''). */
export function normalizeFilters(filters: PublicationsFilters) {
  return {
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? DEFAULT_PAGE_SIZE,
    status: filters.status ?? null,
    tribunal: filters.tribunal || null,
    clienteId: filters.clienteId || null,
    processId: filters.processId || null,
    from: filters.from || null,
    to: filters.to || null,
    isRead: filters.isRead ?? null,
    busca: filters.busca?.trim() || null,
    ordem: filters.ordem ?? 'recent',
  }
}

export const publicationsQuery = (filters: PublicationsFilters = {}, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.publications.list(normalizeFilters(filters)),
    queryFn: () => getPublications(filters, client),
  })

export const publicationsSummaryQuery = (client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.publications.summary(),
    queryFn: () => getPublicationsSummary(client),
    staleTime: 30_000,
  })

export const publicationQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.publications.byId(id),
    queryFn: () => getPublicationById(id, client),
  })
