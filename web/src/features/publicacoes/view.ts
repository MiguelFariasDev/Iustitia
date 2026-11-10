import type { Tone } from '@/components/shared/tones'
import type { UserRole } from '@/lib/auth/types'
import type { Publication, PublicationsFilters } from './types'

/**
 * Visões rápidas da lista (v4): Todas | Não lidas | Aguardando revisão | Tratadas. Cada uma é só
 * uma combinação de `status` e `isRead` — os demais filtros (tribunal, cliente, busca…) seguem
 * valendo por cima, e a visão ativa é DERIVADA dos filtros (a URL continua sendo a única fonte).
 */
export type QuickView = 'all' | 'unread' | 'review' | 'treated'

export const quickViews: readonly { value: QuickView; label: string }[] = [
  { value: 'all', label: 'Todas' },
  { value: 'unread', label: 'Não lidas' },
  { value: 'review', label: 'Aguardando revisão' },
  { value: 'treated', label: 'Tratadas' },
]

/** Filtros que a visão rápida aplica. "Aguardando revisão" é o `untreated` do domínio. */
export function quickViewFilters(view: QuickView): Pick<PublicationsFilters, 'status' | 'isRead'> {
  switch (view) {
    case 'unread':
      return { status: null, isRead: false }
    case 'review':
      return { status: 'untreated', isRead: null }
    case 'treated':
      return { status: 'treated', isRead: null }
    default:
      return { status: null, isRead: null }
  }
}

/** A visão que os filtros atuais representam, ou `null` se for outra combinação (ex.: arquivadas). */
export function quickViewOf(filters: PublicationsFilters): QuickView | null {
  const status = filters.status ?? null
  const isRead = filters.isRead ?? null

  if (status === null && isRead === null) return 'all'
  if (status === null && isRead === false) return 'unread'
  if (status === 'untreated' && isRead === null) return 'review'
  if (status === 'treated' && isRead === null) return 'treated'

  return null
}

export interface StatusBadgeSpec {
  label: string
  tone: Tone
  /** Acompanha o selo ✨ (a IA está envolvida na revisão). */
  ai: boolean
}

/**
 * Selo de status da linha.
 *
 * O contrato da API NÃO informa, por publicação, se a IA sugeriu um prazo. O que existe é o
 * estado (`untreated` = aguardando revisão humana) e a flag `ai.classification` do escritório.
 * Por isso "Revisão da IA" só aparece com a flag ligada, e o texto não afirma que um prazo foi
 * sugerido — dizer "IA sugeriu prazo" numa linha em que ela não sugeriu nada seria pior que não
 * ter o selo. Quando a API expuser a sugestão, este é o único lugar a ajustar.
 */
export function statusBadgeOf(publication: Publication, aiEnabled: boolean): StatusBadgeSpec {
  switch (publication.status) {
    case 'treated':
      return { label: 'Tratada', tone: 'success', ai: false }
    case 'discarded':
      return { label: 'Descartada', tone: 'neutral', ai: false }
    case 'archived':
      return { label: 'Arquivada', tone: 'info', ai: false }
    default:
      if (publication.processId === null)
        return { label: 'Sem processo', tone: 'neutral', ai: false }

      return aiEnabled
        ? { label: 'Revisão da IA', tone: 'info', ai: true }
        : { label: 'Aguardando revisão', tone: 'warning', ai: false }
  }
}

/** Revisar o que a IA sugeriu é ato de advogado ou sócio (Provimento CFOAB 271/2025). */
export function canReviewAi(role: UserRole | null | undefined): boolean {
  return role === 'Owner' || role === 'Partner' || role === 'Lawyer'
}

const pad = (n: number) => String(n).padStart(2, '0')

/** yyyy-MM-dd no fuso LOCAL: é o "hoje" da pessoa, não o do servidor. */
export function isoDate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** Segunda-feira da semana de `date` (a contagem "desde segunda-feira" do resumo). */
export function mondayOf(date: Date): Date {
  const offset = (date.getDay() + 6) % 7

  return new Date(date.getFullYear(), date.getMonth(), date.getDate() - offset)
}
