import type { Alerta, AlertasSummary, AlertType } from './types'
import { alertTypes } from './types'

/** Item de GET /alerts e /alerts/{id} (AlertResponse). */
export interface AlertDto {
  id: string
  type: string
  title: string
  description: string | null
  referenceId: string | null
  isRead: boolean
  createdAt: string
  readAt: string | null
}

export interface AlertsPageDto {
  items: AlertDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** GET /alerts/summary (AlertsSummaryResponse). */
export interface AlertsSummaryDto {
  total: number
  unread: number
  unreadByType: Record<string, number>
  totalByType: Record<string, number>
}

/** Payload do Realtime (AlertCreatedConsumer). */
export interface AlertRealtimePayload {
  alertId: string
  type: string
  title: string
  description: string | null
  referenceId: string | null
  createdAt: string
}

/** Tipo desconhecido vira 'Other': um tipo novo do backend não pode quebrar a tela. */
function toType(value: string): AlertType {
  return alertTypes.find((type) => type === value) ?? 'Other'
}

export function toAlerta(dto: AlertDto): Alerta {
  return {
    id: dto.id,
    type: toType(dto.type),
    title: dto.title,
    description: dto.description,
    referenceId: dto.referenceId,
    isRead: dto.isRead,
    createdAt: dto.createdAt,
    readAt: dto.readAt,
  }
}

function byType(record: Record<string, number>): Partial<Record<AlertType, number>> {
  const result: Partial<Record<AlertType, number>> = {}
  for (const [key, value] of Object.entries(record ?? {})) {
    const type = toType(key)
    result[type] = (result[type] ?? 0) + value
  }
  return result
}

export function toAlertasSummary(dto: AlertsSummaryDto): AlertasSummary {
  return {
    total: dto.total,
    unread: dto.unread,
    unreadByType: byType(dto.unreadByType),
    totalByType: byType(dto.totalByType),
  }
}

/** Valida o payload vindo do canal: dado de rede nunca é confiável só por vir do nosso backend. */
export function parseRealtimePayload(value: unknown): AlertRealtimePayload | null {
  if (typeof value !== 'object' || value === null) return null
  const data = value as Record<string, unknown>

  if (typeof data.alertId !== 'string' || typeof data.title !== 'string') return null

  return {
    alertId: data.alertId,
    type: typeof data.type === 'string' ? data.type : 'Other',
    title: data.title,
    description: typeof data.description === 'string' ? data.description : null,
    referenceId: typeof data.referenceId === 'string' ? data.referenceId : null,
    createdAt: typeof data.createdAt === 'string' ? data.createdAt : new Date().toISOString(),
  }
}
