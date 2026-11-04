import type { Tone } from './tones'

export type DeadlineUrgency = 'overdue' | 'today' | 'critical' | 'attention' | 'ok'

const DAY = 24 * 60 * 60 * 1000

function startOfDay(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
}

/** Dias corridos entre hoje e o vencimento (negativo = vencido). */
export function daysUntil(dueDate: Date, now: Date = new Date()) {
  return Math.round((startOfDay(dueDate) - startOfDay(now)) / DAY)
}

/**
 * Faixas de urgência de prazo: vencido | hoje | crítico (1–2 dias) | atenção (3–5) | ok (6+).
 * Prazos processuais são o maior risco do escritório — em caso de dúvida, a UI deve errar
 * para o lado do alerta.
 */
export function getDeadlineUrgency(dueDate: Date, now: Date = new Date()): DeadlineUrgency {
  const days = daysUntil(dueDate, now)
  if (days < 0) return 'overdue'
  if (days === 0) return 'today'
  if (days <= 2) return 'critical'
  if (days <= 5) return 'attention'
  return 'ok'
}

export const urgencyTone: Record<DeadlineUrgency, Tone> = {
  overdue: 'danger',
  today: 'danger',
  critical: 'danger',
  attention: 'warning',
  ok: 'success',
}

export function getDeadlineLabel(dueDate: Date, now: Date = new Date()) {
  const days = daysUntil(dueDate, now)
  if (days < -1) return `Vencido há ${Math.abs(days)} dias`
  if (days === -1) return 'Venceu ontem'
  if (days === 0) return 'Vence hoje'
  if (days === 1) return 'Vence amanhã'
  return `Vence em ${days} dias`
}
