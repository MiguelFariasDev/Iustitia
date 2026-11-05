import {
  addMonths,
  eventosDoDia,
  isSameDay,
  monthGrid,
  moveToDay,
  moveToSlot,
  periodFor,
  shiftAnchor,
  startOfWeek,
  titleFor,
  toDateTimeLocal,
  weekDays,
} from './dates'
import type { AgendaEvento } from './types'

const evento = (inicio: Date, fim: Date, extra: Partial<AgendaEvento> = {}): AgendaEvento => ({
  id: 'e1',
  titulo: 'Audiência',
  descricao: null,
  tipo: 'Audiencia',
  inicio: inicio.toISOString(),
  fim: fim.toISOString(),
  diaInteiro: false,
  local: null,
  processoId: null,
  responsavelId: null,
  ...extra,
})

describe('semana', () => {
  it('começa na segunda-feira, inclusive quando a data é domingo', () => {
    expect(startOfWeek(new Date(2026, 10, 8)).getDate()).toBe(2) // dom 08/nov → seg 02/nov
    expect(startOfWeek(new Date(2026, 10, 2)).getDate()).toBe(2) // seg 02/nov
    expect(weekDays(new Date(2026, 10, 4))).toHaveLength(7)
  })
})

describe('monthGrid', () => {
  it('sempre devolve semanas completas, começando na segunda', () => {
    const grid = monthGrid(new Date(2026, 10, 15)) // novembro/2026

    expect(grid.every((week) => week.length === 7)).toBe(true)
    expect(grid[0]![0]!.getDay()).toBe(1)
    expect(grid.flat().some((d) => d.getMonth() === 10 && d.getDate() === 30)).toBe(true)
  })

  it('cobre um mês que ocupa 6 semanas', () => {
    expect(monthGrid(new Date(2026, 7, 1))).toHaveLength(6) // agosto/2026 começa no sábado
  })
})

describe('navegação', () => {
  it('somar um mês a 31/jan não pula para março', () => {
    expect(addMonths(new Date(2026, 0, 31), 1).getMonth()).toBe(1)
  })

  it('avança e recua conforme a visão', () => {
    const base = new Date(2026, 10, 10)

    expect(shiftAnchor('dia', base, 1).getDate()).toBe(11)
    expect(shiftAnchor('semana', base, -1).getDate()).toBe(3)
    expect(shiftAnchor('mes', base, 1).getMonth()).toBe(11)
  })
})

describe('periodFor', () => {
  it('a janela do dia tem 24h', () => {
    const { from, to } = periodFor('dia', new Date(2026, 10, 10, 15))

    expect(new Date(to).getTime() - new Date(from).getTime()).toBe(86_400_000)
  })

  it('a janela do mês cobre as semanas vizinhas e não passa do teto de 62 dias do servidor', () => {
    const { from, to } = periodFor('mes', new Date(2026, 7, 15))
    const dias = (new Date(to).getTime() - new Date(from).getTime()) / 86_400_000

    expect(dias).toBeLessThanOrEqual(62)
    expect(new Date(from).getTime()).toBeLessThanOrEqual(new Date(2026, 7, 1).getTime())
  })
})

describe('eventosDoDia', () => {
  it('um evento de vários dias aparece em todos eles', () => {
    const e = evento(new Date(2026, 10, 10, 9), new Date(2026, 10, 12, 18))

    expect(eventosDoDia([e], new Date(2026, 10, 11))).toHaveLength(1)
    expect(eventosDoDia([e], new Date(2026, 10, 13))).toHaveLength(0)
  })

  it('evento que termina à meia-noite não vaza para o dia seguinte', () => {
    const e = evento(new Date(2026, 10, 10, 22), new Date(2026, 10, 11, 0))

    expect(eventosDoDia([e], new Date(2026, 10, 11))).toHaveLength(0)
  })

  it('ordena por início', () => {
    const tarde = evento(new Date(2026, 10, 10, 15), new Date(2026, 10, 10, 16), { id: 'b' })
    const cedo = evento(new Date(2026, 10, 10, 8), new Date(2026, 10, 10, 9), { id: 'a' })

    expect(eventosDoDia([tarde, cedo], new Date(2026, 10, 10)).map((e) => e.id)).toEqual(['a', 'b'])
  })
})

describe('mover (arrastar e soltar)', () => {
  const e = evento(new Date(2026, 10, 10, 14, 30), new Date(2026, 10, 10, 16, 30))

  it('moveToDay troca o dia e preserva hora e duração', () => {
    const { inicio, fim } = moveToDay(e, new Date(2026, 10, 20))

    expect(new Date(inicio).getDate()).toBe(20)
    expect(new Date(inicio).getHours()).toBe(14)
    expect(new Date(inicio).getMinutes()).toBe(30)
    expect(new Date(fim).getTime() - new Date(inicio).getTime()).toBe(2 * 3_600_000)
  })

  it('moveToSlot define a hora cheia do slot e preserva a duração', () => {
    const { inicio, fim } = moveToSlot(e, new Date(2026, 10, 12), 9)

    expect(new Date(inicio).getHours()).toBe(9)
    expect(new Date(inicio).getMinutes()).toBe(0)
    expect(new Date(fim).getHours()).toBe(11)
  })
})

describe('formatação', () => {
  it('toDateTimeLocal usa a hora local', () => {
    expect(toDateTimeLocal(new Date(2026, 10, 4, 9, 5))).toBe('2026-11-04T09:05')
  })

  it('isSameDay ignora a hora', () => {
    expect(isSameDay(new Date(2026, 10, 4, 1), new Date(2026, 10, 4, 23))).toBe(true)
  })

  it('titleFor capitaliza o mês', () => {
    expect(titleFor('mes', new Date(2026, 10, 4))).toBe('Novembro de 2026')
  })
})
