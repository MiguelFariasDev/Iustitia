import type { AgendaEvento, TipoEvento } from '@/features/agenda/types'
import { addDays, isSameDay, startOfDay } from './time'

/**
 * Fila de trabalho priorizada do painel: o que vence hoje e nos próximos dias, mais as
 * publicações que a IA leu e esperam revisão do advogado.
 *
 * Só entra o que existe de verdade: os prazos vêm da agenda e as revisões, das publicações.
 * NÃO há "Atrasado": a agenda não guarda se um compromisso foi cumprido, então um prazo de
 * ontem sem baixa seria indistinguível de um prazo que o escritório perdeu — e classificar tudo
 * que passou como atrasado inventaria um alarme. O nível `overdue` existe para quando houver
 * módulo de tarefas com conclusão.
 */
export type QueueUrgency = 'overdue' | 'today' | 'tomorrow' | 'week' | 'review'

export interface QueueItem {
  id: string
  kind: TipoEvento | 'Revisao'
  title: string
  /** Número do processo (mono) quando o evento está vinculado a um e o número é conhecido. */
  processLabel: string | null
  /** ISO do compromisso; `null` na revisão de publicação, que não tem data própria. */
  due: string | null
  urgency: QueueUrgency
  /** Veio da IA (sugestão a revisar): recebe o selo ✨. */
  ai: boolean
  href: string
}

export type QueueFilter = 'all' | 'today' | 'ai'

export interface ReviewPublication {
  id: string
  processNumber: string | null
  /** Tribunal ou diário de origem, para o título da linha. */
  source: string
}

const URGENCY_RANK: Record<QueueUrgency, number> = {
  overdue: 0,
  today: 1,
  tomorrow: 2,
  week: 3,
  review: 4,
}

/** Quantos dias à frente a fila olha. */
export const QUEUE_WINDOW_DAYS = 7

export function urgencyOf(due: Date, now: Date): QueueUrgency {
  if (isSameDay(due, now)) return 'today'
  if (isSameDay(due, addDays(now, 1))) return 'tomorrow'

  return 'week'
}

export function buildQueue(input: {
  events: readonly AgendaEvento[]
  processNumbers: ReadonlyMap<string, string>
  reviews: readonly ReviewPublication[]
  now: Date
}): QueueItem[] {
  const { events, processNumbers, reviews, now } = input
  const from = startOfDay(now)
  const to = addDays(from, QUEUE_WINDOW_DAYS + 1)

  const dated: QueueItem[] = events
    .filter((evento) => {
      const inicio = new Date(evento.inicio)
      return inicio >= from && inicio < to
    })
    .map((evento) => ({
      id: `evento-${evento.id}`,
      kind: evento.tipo,
      title: evento.titulo,
      processLabel: evento.processoId ? (processNumbers.get(evento.processoId) ?? null) : null,
      due: evento.inicio,
      urgency: urgencyOf(new Date(evento.inicio), now),
      ai: false,
      href: '/agenda',
    }))
    .sort(
      (a, b) => URGENCY_RANK[a.urgency] - URGENCY_RANK[b.urgency] || a.due!.localeCompare(b.due!)
    )

  const revisoes: QueueItem[] = reviews.map((publication) => ({
    id: `revisao-${publication.id}`,
    kind: 'Revisao',
    title: `Revisar sugestão de prazo · ${publication.source}`,
    processLabel: publication.processNumber,
    due: null,
    urgency: 'review',
    ai: true,
    href: `/publicacoes/${publication.id}`,
  }))

  return [...dated, ...revisoes]
}

export function filterQueue(items: readonly QueueItem[], filter: QueueFilter): QueueItem[] {
  if (filter === 'today') {
    return items.filter((item) => item.urgency === 'today' || item.urgency === 'overdue')
  }
  if (filter === 'ai') return items.filter((item) => item.ai)

  return [...items]
}

/** Prazos (tipo Prazo) que vencem no dia de `now`. */
export function prazosDoDia(events: readonly AgendaEvento[], now: Date): AgendaEvento[] {
  return events
    .filter((evento) => evento.tipo === 'Prazo' && isSameDay(new Date(evento.inicio), now))
    .sort((a, b) => a.inicio.localeCompare(b.inicio))
}

/** Prazos (tipo Prazo) de hoje até `QUEUE_WINDOW_DAYS` dias à frente, em ordem de data. */
export function prazosDaSemana(events: readonly AgendaEvento[], now: Date): AgendaEvento[] {
  const from = startOfDay(now)
  const to = addDays(from, QUEUE_WINDOW_DAYS + 1)

  return events
    .filter((evento) => {
      const inicio = new Date(evento.inicio)
      return evento.tipo === 'Prazo' && inicio >= from && inicio < to
    })
    .sort((a, b) => a.inicio.localeCompare(b.inicio))
}
