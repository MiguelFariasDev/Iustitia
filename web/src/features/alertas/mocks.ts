import { minutesAgo } from '@/mocks/dates'
import type { AlertRealtimePayload } from './contract'
import type { Alerta, AlertasFilters, AlertasPage, AlertasSummary, AlertType } from './types'

/**
 * Backend simulado, EM MEMÓRIA, para dev e E2E sem a API .NET (NEXT_PUBLIC_API_MOCKS=true).
 * Reimplementa as regras da API: alerta descartado some, "já lido" é conflito, filtros por tipo
 * (vários) e por leitura. Uma recarga volta ao estado inicial.
 */
function seed(): Alerta[] {
  const base = (
    id: string,
    type: AlertType,
    title: string,
    description: string,
    minutes: number,
    isRead = false
  ): Alerta => ({
    id,
    type,
    title,
    description,
    referenceId: type === 'Publication' ? `pub-${id.slice(-3)}` : null,
    isRead,
    createdAt: minutesAgo(minutes),
    readAt: isRead ? minutesAgo(minutes - 1) : null,
  })

  return [
    base(
      'alert-001',
      'Publication',
      'Nova publicação: intimação',
      'Processo 5001001-10.2026.8.26.0001 — TJSP',
      12
    ),
    base(
      'alert-002',
      'Publication',
      'Nova publicação: sentença',
      'Processo 5001002-11.2026.8.26.0001 — TJSP',
      95
    ),
    base('alert-003', 'Task', 'Tarefa vence amanhã', 'Revisar minuta da contestação', 180),
    base(
      'alert-004',
      'Birthday',
      'Aniversário de cliente',
      'Maria Aparecida Silva faz aniversário hoje',
      600
    ),
    base(
      'alert-005',
      'Publication',
      'Nova publicação',
      'Processo 5001005-14.2026.8.26.0001 — TJRJ',
      60 * 30,
      true
    ),
    base(
      'alert-006',
      'Other',
      'Backup concluído',
      'Cópia de segurança do escritório finalizada',
      60 * 48,
      true
    ),
  ]
}

let store: Alerta[] = seed()
/** Alertas descartados saem das listagens, mas o id continua conhecido (descartar de novo = conflito). */
const dismissed = new Set<string>()

export function resetAlertasMock() {
  store = seed()
  dismissed.clear()
}

const visible = () => store.filter((alert) => !dismissed.has(alert.id))

function matches(alert: Alerta, filters: AlertasFilters): boolean {
  if (filters.types && filters.types.length > 0 && !filters.types.includes(alert.type)) return false
  if (filters.isRead !== null && filters.isRead !== undefined && alert.isRead !== filters.isRead) {
    return false
  }
  const day = alert.createdAt.slice(0, 10)
  if (filters.from && day < filters.from) return false
  if (filters.to && day > filters.to) return false
  return true
}

export function mockListAlertas(filters: AlertasFilters): AlertasPage {
  const page = filters.page && filters.page > 0 ? filters.page : 1
  const pageSize = filters.pageSize && filters.pageSize > 0 ? filters.pageSize : 20
  const all = visible()
    .filter((alert) => matches(alert, filters))
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))

  return {
    items: all.slice((page - 1) * pageSize, page * pageSize).map((alert) => ({ ...alert })),
    page,
    pageSize,
    totalCount: all.length,
    totalPages: Math.ceil(all.length / pageSize),
  }
}

function countByType(alerts: Alerta[]) {
  const result: Partial<Record<AlertType, number>> = {}
  for (const alert of alerts) result[alert.type] = (result[alert.type] ?? 0) + 1
  return result
}

export function mockAlertasSummary(): AlertasSummary {
  const all = visible()
  const unread = all.filter((alert) => !alert.isRead)

  return {
    total: all.length,
    unread: unread.length,
    unreadByType: countByType(unread),
    totalByType: countByType(all),
  }
}

export function mockGetAlerta(id: string): Alerta | null {
  const found = visible().find((alert) => alert.id === id)
  return found ? { ...found } : null
}

export function mockMarkAlertaAsRead(id: string): 'ok' | 'not-found' | 'already-read' {
  const found = visible().find((alert) => alert.id === id)
  if (!found) return 'not-found'
  if (found.isRead) return 'already-read'
  found.isRead = true
  found.readAt = new Date().toISOString()
  return 'ok'
}

export function mockMarkAllAlertasAsRead(): number {
  const unread = visible().filter((alert) => !alert.isRead)
  for (const alert of unread) {
    alert.isRead = true
    alert.readAt = new Date().toISOString()
  }
  return unread.length
}

export function mockDismissAlerta(id: string): 'ok' | 'not-found' | 'already-dismissed' {
  if (dismissed.has(id)) return 'already-dismissed'
  const found = store.find((alert) => alert.id === id)
  if (!found) return 'not-found'
  found.isRead = true
  dismissed.add(id)
  return 'ok'
}

/** Simula o que o Realtime faria: um alerta novo chega. Usado pelo E2E de notificação. */
export function mockPushAlerta(payload: AlertRealtimePayload) {
  store.unshift({
    id: payload.alertId,
    type:
      (['Publication', 'Task', 'Birthday', 'Other'] as const).find((t) => t === payload.type) ??
      'Other',
    title: payload.title,
    description: payload.description,
    referenceId: payload.referenceId,
    isRead: false,
    createdAt: payload.createdAt,
    readAt: null,
  })
}
