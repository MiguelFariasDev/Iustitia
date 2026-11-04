import { daysUntil, getDeadlineLabel, getDeadlineUrgency } from './deadline'

const now = new Date(2026, 8, 23, 15, 30) // 23/09/2026 15:30
const day = (offset: number) => new Date(2026, 8, 23 + offset, 9, 0)

describe('prazos', () => {
  it('conta dias corridos ignorando o horário', () => {
    expect(daysUntil(day(0), now)).toBe(0)
    expect(daysUntil(day(1), now)).toBe(1)
    expect(daysUntil(day(-3), now)).toBe(-3)
  })

  it.each([
    [-2, 'overdue'],
    [0, 'today'],
    [1, 'critical'],
    [2, 'critical'],
    [3, 'attention'],
    [5, 'attention'],
    [6, 'ok'],
  ] as const)('prazo em %i dia(s) → %s', (offset, urgency) => {
    expect(getDeadlineUrgency(day(offset), now)).toBe(urgency)
  })

  it.each([
    [-5, 'Vencido há 5 dias'],
    [-1, 'Venceu ontem'],
    [0, 'Vence hoje'],
    [1, 'Vence amanhã'],
    [4, 'Vence em 4 dias'],
  ])('rótulo para %i dia(s): %s', (offset, label) => {
    expect(getDeadlineLabel(day(offset), now)).toBe(label)
  })
})
