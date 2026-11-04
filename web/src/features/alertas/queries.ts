import { queryOptions } from '@tanstack/react-query'
import type { AxiosInstance } from 'axios'
import { queryKeys } from '@/lib/query/queryKeys'
import { DEFAULT_ALERTAS_PAGE_SIZE, getAlertaById, getAlertas, getAlertasSummary } from './api'
import type { AlertasFilters } from './types'

/** Normaliza para a chave não mudar por diferença irrelevante (undefined vs null vs []). */
export function normalizeAlertasFilters(filters: AlertasFilters) {
  return {
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? DEFAULT_ALERTAS_PAGE_SIZE,
    types: filters.types && filters.types.length > 0 ? [...filters.types].sort() : null,
    isRead: filters.isRead ?? null,
    from: filters.from || null,
    to: filters.to || null,
  }
}

export const alertasQuery = (filters: AlertasFilters = {}, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.alerts.list(normalizeAlertasFilters(filters)),
    queryFn: () => getAlertas(filters, client),
  })

/**
 * `refetchInterval` é a rede de segurança do Realtime: se o canal cair ou nunca conectar (dev
 * sem Supabase), o sino ainda converge em um minuto. Com o canal ativo, o push invalida antes.
 */
export const alertasSummaryQuery = (client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.alerts.summary(),
    queryFn: () => getAlertasSummary(client),
    staleTime: 30_000,
    refetchInterval: 60_000,
  })

export const alertaQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.alerts.byId(id),
    queryFn: () => getAlertaById(id, client),
  })
