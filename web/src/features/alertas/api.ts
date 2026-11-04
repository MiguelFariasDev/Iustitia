import type { AxiosInstance } from 'axios'
import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import {
  toAlerta,
  toAlertasSummary,
  type AlertDto,
  type AlertsPageDto,
  type AlertsSummaryDto,
} from './contract'
import {
  mockDismissAlerta,
  mockGetAlerta,
  mockListAlertas,
  mockMarkAlertaAsRead,
  mockMarkAllAlertasAsRead,
  mockAlertasSummary,
} from './mocks'
import type { Alerta, AlertasFilters, AlertasPage, AlertasSummary } from './types'

export const DEFAULT_ALERTAS_PAGE_SIZE = 20

export async function getAlertas(
  filters: AlertasFilters = {},
  client: AxiosInstance | undefined = apiClient
): Promise<AlertasPage> {
  const normalized = { ...filters, pageSize: filters.pageSize ?? DEFAULT_ALERTAS_PAGE_SIZE }
  if (env.useMocks || !client) return mockResponse(mockListAlertas(normalized), 250)

  const { data } = await client.get<AlertsPageDto>(endpoints.alerts.list, {
    params: {
      page: normalized.page ?? 1,
      pageSize: normalized.pageSize,
      type:
        normalized.types && normalized.types.length > 0 ? normalized.types.join(',') : undefined,
      isRead: normalized.isRead ?? undefined,
      from: normalized.from || undefined,
      to: normalized.to || undefined,
    },
  })

  return { ...data, items: data.items.map(toAlerta) }
}

export async function getAlertasSummary(
  client: AxiosInstance | undefined = apiClient
): Promise<AlertasSummary> {
  if (env.useMocks || !client) return mockResponse(mockAlertasSummary(), 100)

  const { data } = await client.get<AlertsSummaryDto>(endpoints.alerts.summary)

  return toAlertasSummary(data)
}

export async function getAlertaById(
  id: string,
  client: AxiosInstance | undefined = apiClient
): Promise<Alerta> {
  if (env.useMocks || !client) {
    const found = mockGetAlerta(id)
    if (!found)
      throw new ApiError(404, { errorCode: 'ALERT_NOT_FOUND', detail: 'Alerta não encontrado.' })
    return mockResponse(found, 150)
  }

  const { data } = await client.get<AlertDto>(endpoints.alerts.byId(id))

  return toAlerta(data)
}

export async function markAlertaAsRead(id: string): Promise<void> {
  if (env.useMocks) {
    const result = mockMarkAlertaAsRead(id)
    if (result === 'not-found') throw new ApiError(404, { errorCode: 'ALERT_NOT_FOUND' })
    if (result === 'already-read') throw new ApiError(409, { errorCode: 'ALERT_ALREADY_READ' })
    return mockResponse(undefined, 80)
  }

  await apiClient.patch(endpoints.alerts.read(id))
}

/** Devolve quantos alertas mudaram de estado. */
export async function markAllAlertasAsRead(): Promise<number> {
  if (env.useMocks) return mockResponse(mockMarkAllAlertasAsRead(), 100)

  const { data } = await apiClient.patch<number>(endpoints.alerts.readAll)

  return data
}

export async function dismissAlerta(id: string): Promise<void> {
  if (env.useMocks) {
    const result = mockDismissAlerta(id)
    if (result === 'not-found') throw new ApiError(404, { errorCode: 'ALERT_NOT_FOUND' })
    if (result === 'already-dismissed')
      throw new ApiError(409, { errorCode: 'ALERT_ALREADY_DISMISSED' })
    return mockResponse(undefined, 80)
  }

  await apiClient.delete(endpoints.alerts.dismiss(id))
}
