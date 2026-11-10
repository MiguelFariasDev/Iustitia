import type { Publication, PublicationStatus } from './types'
import {
  canReviewAi,
  isoDate,
  mondayOf,
  quickViewFilters,
  quickViewOf,
  statusBadgeOf,
} from './view'

const pub = (status: PublicationStatus, processId: string | null = 'p1'): Publication =>
  ({ status, processId }) as Publication

describe('visões rápidas', () => {
  it.each(['all', 'unread', 'review', 'treated'] as const)(
    'ida e volta: aplicar "%s" e reconhecê-la nos filtros',
    (view) => {
      expect(quickViewOf({ ...quickViewFilters(view) })).toBe(view)
    }
  )

  it('"Aguardando revisão" é o status untreated do domínio', () => {
    expect(quickViewFilters('review')).toEqual({ status: 'untreated', isRead: null })
  })

  it('outros filtros (tribunal, busca) não mudam a visão', () => {
    expect(quickViewOf({ status: 'treated', tribunal: 'TJCE', busca: 'silva' })).toBe('treated')
  })

  it('combinação que nenhuma visão representa devolve null (nenhum botão marcado)', () => {
    expect(quickViewOf({ status: 'archived' })).toBeNull()
    expect(quickViewOf({ status: 'untreated', isRead: false })).toBeNull()
  })

  it('"Não lidas" não filtra por status', () => {
    expect(quickViewFilters('unread')).toEqual({ status: null, isRead: false })
  })
})

describe('statusBadgeOf', () => {
  it('tratada, descartada e arquivada', () => {
    expect(statusBadgeOf(pub('treated'), true)).toMatchObject({ label: 'Tratada', tone: 'success' })
    expect(statusBadgeOf(pub('discarded'), true).label).toBe('Descartada')
    expect(statusBadgeOf(pub('archived'), true).label).toBe('Arquivada')
  })

  it('sem processo vinculado, não há o que revisar com a IA: "Sem processo"', () => {
    expect(statusBadgeOf(pub('untreated', null), true)).toMatchObject({
      label: 'Sem processo',
      ai: false,
    })
  })

  it('com a IA ligada, a pendente vira "Revisão da IA" — sem afirmar que sugeriu um prazo', () => {
    const spec = statusBadgeOf(pub('untreated'), true)

    expect(spec).toMatchObject({ label: 'Revisão da IA', tone: 'info', ai: true })
    expect(spec.label).not.toMatch(/sugeriu/i)
  })

  it('com a IA desligada, a pendente é só "Aguardando revisão", sem selo de IA', () => {
    expect(statusBadgeOf(pub('untreated'), false)).toMatchObject({
      label: 'Aguardando revisão',
      tone: 'warning',
      ai: false,
    })
  })
})

describe('canReviewAi', () => {
  it('só advogado e sócio', () => {
    expect(canReviewAi('Owner')).toBe(true)
    expect(canReviewAi('Partner')).toBe(true)
    expect(canReviewAi('Lawyer')).toBe(true)
    for (const papel of ['Intern', 'Admin', 'Financial', null, undefined] as const) {
      expect(canReviewAi(papel)).toBe(false)
    }
  })
})

describe('datas locais', () => {
  it('isoDate usa o dia local, com zero à esquerda', () => {
    expect(isoDate(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05')
  })

  it('mondayOf: segunda é ela mesma; domingo volta 6 dias', () => {
    expect(isoDate(mondayOf(new Date(2026, 10, 2)))).toBe('2026-11-02') // segunda
    expect(isoDate(mondayOf(new Date(2026, 10, 5)))).toBe('2026-11-02') // quinta
    expect(isoDate(mondayOf(new Date(2026, 10, 8)))).toBe('2026-11-02') // domingo
  })

  it('mondayOf atravessa a virada de mês', () => {
    expect(isoDate(mondayOf(new Date(2026, 11, 2)))).toBe('2026-11-30') // quarta, 2/dez
  })
})
