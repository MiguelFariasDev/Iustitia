import type { AgendaEvento, AgendaPeriodo, VisaoAgenda } from './types'

/**
 * Aritmética de datas da agenda, sempre em hora LOCAL (a agenda é do dia do usuário, não do
 * dia UTC). Semana começa na segunda-feira, como no calendário forense brasileiro.
 */

export const diasSemanaCurtos = ['seg', 'ter', 'qua', 'qui', 'sex', 'sáb', 'dom'] as const

export function startOfDay(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate())
}

export function addDays(date: Date, days: number): Date {
  return new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate() + days,
    date.getHours(),
    date.getMinutes()
  )
}

export function addMonths(date: Date, months: number): Date {
  // Dia 1 antes de somar: 31/jan + 1 mês não pode virar 3/mar.
  return new Date(date.getFullYear(), date.getMonth() + months, 1)
}

/** Segunda-feira da semana de `date`, à meia-noite. */
export function startOfWeek(date: Date): Date {
  const day = startOfDay(date)
  const offset = (day.getDay() + 6) % 7 // segunda = 0 … domingo = 6

  return addDays(day, -offset)
}

export function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  )
}

export function weekDays(date: Date): Date[] {
  const start = startOfWeek(date)

  return Array.from({ length: 7 }, (_, i) => addDays(start, i))
}

/** Grade do mês: sempre semanas completas (seg→dom), de 5 ou 6 linhas. */
export function monthGrid(date: Date): Date[][] {
  const first = new Date(date.getFullYear(), date.getMonth(), 1)
  const gridStart = startOfWeek(first)
  const lastOfMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0)
  const weeks = Math.ceil(
    ((startOfWeek(lastOfMonth).getTime() - gridStart.getTime()) / 86_400_000 + 7) / 7
  )

  return Array.from({ length: weeks }, (_, w) =>
    Array.from({ length: 7 }, (_, d) => addDays(gridStart, w * 7 + d))
  )
}

/** Janela [from, to) que a visão precisa carregar. */
export function periodFor(view: VisaoAgenda, anchor: Date): AgendaPeriodo {
  if (view === 'dia') {
    const start = startOfDay(anchor)

    return { from: start.toISOString(), to: addDays(start, 1).toISOString() }
  }

  if (view === 'semana') {
    const start = startOfWeek(anchor)

    return { from: start.toISOString(), to: addDays(start, 7).toISOString() }
  }

  const grid = monthGrid(anchor)
  const start = grid[0]![0]!
  const end = addDays(grid[grid.length - 1]![6]!, 1)

  return { from: start.toISOString(), to: end.toISOString() }
}

/** Move a âncora um passo (dia, semana ou mês) para frente (+1) ou para trás (-1). */
export function shiftAnchor(view: VisaoAgenda, anchor: Date, direction: 1 | -1): Date {
  if (view === 'dia') return addDays(anchor, direction)
  if (view === 'semana') return addDays(anchor, 7 * direction)

  return addMonths(anchor, direction)
}

const mesLongo = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' })
const diaLongo = new Intl.DateTimeFormat('pt-BR', {
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
})
const diaMes = new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'short' })
const horaCurta = new Intl.DateTimeFormat('pt-BR', { hour: '2-digit', minute: '2-digit' })

export function titleFor(view: VisaoAgenda, anchor: Date): string {
  if (view === 'mes') return capitalize(mesLongo.format(anchor))
  if (view === 'dia') return capitalize(diaLongo.format(anchor))

  const days = weekDays(anchor)

  return `${diaMes.format(days[0]!)} – ${diaMes.format(days[6]!)} de ${days[6]!.getFullYear()}`
}

export function formatHora(iso: string): string {
  return horaCurta.format(new Date(iso))
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** Eventos que tocam o dia `day` (um evento de vários dias aparece em cada um deles). */
export function eventosDoDia(eventos: readonly AgendaEvento[], day: Date): AgendaEvento[] {
  const start = startOfDay(day).getTime()
  const end = addDays(startOfDay(day), 1).getTime()

  return eventos
    .filter((e) => {
      const inicio = new Date(e.inicio).getTime()
      const fim = new Date(e.fim).getTime()
      // Fim exclusivo do dia; evento de duração zero conta no dia em que começa.
      return inicio < end && (fim > start || inicio >= start)
    })
    .sort((a, b) => a.inicio.localeCompare(b.inicio))
}

/** `Date` → valor de <input type="datetime-local"> (hora local, sem fuso). */
export function toDateTimeLocal(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')

  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/** Mantém a hora/duração do evento ao movê-lo para outro dia (arrastar no mês). */
export function moveToDay(evento: AgendaEvento, day: Date): { inicio: string; fim: string } {
  const inicio = new Date(evento.inicio)
  const duracao = new Date(evento.fim).getTime() - inicio.getTime()
  const novoInicio = new Date(
    day.getFullYear(),
    day.getMonth(),
    day.getDate(),
    inicio.getHours(),
    inicio.getMinutes()
  )

  return {
    inicio: novoInicio.toISOString(),
    fim: new Date(novoInicio.getTime() + duracao).toISOString(),
  }
}

/** Move para um horário específico do dia (arrastar na semana/dia), mantendo a duração. */
export function moveToSlot(
  evento: AgendaEvento,
  day: Date,
  hour: number
): { inicio: string; fim: string } {
  const duracao = new Date(evento.fim).getTime() - new Date(evento.inicio).getTime()
  const novoInicio = new Date(day.getFullYear(), day.getMonth(), day.getDate(), hour, 0)

  return {
    inicio: novoInicio.toISOString(),
    fim: new Date(novoInicio.getTime() + duracao).toISOString(),
  }
}
