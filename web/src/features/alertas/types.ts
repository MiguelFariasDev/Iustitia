/** Espelha Platform.Domain.Alerts.AlertType. */
export type AlertType = 'Publication' | 'Task' | 'Birthday' | 'Other'

export const alertTypes: AlertType[] = ['Publication', 'Task', 'Birthday', 'Other']

export interface Alerta {
  id: string
  type: AlertType
  title: string
  description: string | null
  /** Id da entidade que originou o alerta (a publicação, quando type = Publication). */
  referenceId: string | null
  isRead: boolean
  createdAt: string
  readAt: string | null
}

export interface AlertasSummary {
  total: number
  unread: number
  unreadByType: Partial<Record<AlertType, number>>
  totalByType: Partial<Record<AlertType, number>>
}

/** Abas da tela: cada uma é um PRESET de filtro (ver tabFilters). */
export type AlertaTab = 'important' | 'all' | 'publications' | 'others'

export const alertaTabs: AlertaTab[] = ['important', 'all', 'publications', 'others']

export interface AlertasFilters {
  page?: number
  pageSize?: number
  /** Um ou mais tipos. Vazio/ausente = todos. */
  types?: AlertType[]
  isRead?: boolean | null
  /** ISO yyyy-MM-dd, inclusivas. */
  from?: string | null
  to?: string | null
}

export interface AlertasPage {
  items: Alerta[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
