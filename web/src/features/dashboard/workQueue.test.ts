import type { AgendaEvento } from '@/features/agenda/types'
import {
  buildQueue,
  filterQueue,
  prazosDaSemana,
  prazosDoDia,
  QUEUE_WINDOW_DAYS,
  urgencyOf,
} from './workQueue'

const NOW = new Date(2026, 10, 5, 10, 0) // qui, 5/nov/2026 10:00

const evento = (
  id: string,
  dias: number,
  hora: number,
  extra: Partial<AgendaEvento> = {}
): AgendaEvento => ({
  id,
  titulo: `Evento ${id}`,
  descricao: null,
  tipo: 'Prazo',
  inicio: new Date(2026, 10, 5 + dias, hora).toISOString(),
  fim: new Date(2026, 10, 5 + dias, hora + 1).toISOString(),
  diaInteiro: false,
  local: null,
  processoId: null,
  responsavelId: null,
  ...extra,
})

const vazio = new Map<string, string>()

describe('urgencyOf', () => {
  it('hoje, amanhã e esta semana', () => {
    expect(urgencyOf(new Date(2026, 10, 5, 23, 59), NOW)).toBe('today')
    expect(urgencyOf(new Date(2026, 10, 6, 0, 1), NOW)).toBe('tomorrow')
    expect(urgencyOf(new Date(2026, 10, 9), NOW)).toBe('week')
  })
})

describe('buildQueue', () => {
  it('ordena por urgência e, dentro dela, por horário', () => {
    const fila = buildQueue({
      events: [evento('c', 3, 9), evento('a', 0, 18), evento('b', 0, 9), evento('d', 1, 8)],
      processNumbers: vazio,
      reviews: [],
      aiEnabled: true,
      now: NOW,
    })

    expect(fila.map((item) => item.id)).toEqual(['evento-b', 'evento-a', 'evento-d', 'evento-c'])
    expect(fila.map((item) => item.urgency)).toEqual(['today', 'today', 'tomorrow', 'week'])
  })

  it('ignora o que já passou: sem baixa de cumprimento não dá para chamar de atrasado', () => {
    const fila = buildQueue({
      events: [evento('ontem', -1, 9), evento('hoje', 0, 9)],
      processNumbers: vazio,
      reviews: [],
      aiEnabled: true,
      now: NOW,
    })

    expect(fila.map((item) => item.id)).toEqual(['evento-hoje'])
    expect(fila.some((item) => item.urgency === 'overdue')).toBe(false)
  })

  it('não olha além da janela de 7 dias', () => {
    const fila = buildQueue({
      events: [evento('limite', QUEUE_WINDOW_DAYS, 9), evento('fora', QUEUE_WINDOW_DAYS + 1, 9)],
      processNumbers: vazio,
      reviews: [],
      aiEnabled: true,
      now: NOW,
    })

    expect(fila.map((item) => item.id)).toEqual(['evento-limite'])
  })

  it('resolve o número do processo pelo vínculo do evento', () => {
    const fila = buildQueue({
      events: [evento('a', 0, 9, { processoId: 'p1' }), evento('b', 0, 10, { processoId: 'p9' })],
      processNumbers: new Map([['p1', '0000777-07.2018.8.06.0100']]),
      reviews: [],
      aiEnabled: true,
      now: NOW,
    })

    expect(fila[0]!.processLabel).toBe('0000777-07.2018.8.06.0100')
    expect(fila[1]!.processLabel).toBeNull()
  })

  it('sem a IA ligada, a revisão é só "a revisar": sem selo de IA e sem dizer que sugeriu prazo', () => {
    const fila = buildQueue({
      events: [],
      processNumbers: vazio,
      reviews: [{ id: 'pub1', processNumber: null, source: 'TJCE' }],
      aiEnabled: false,
      now: NOW,
    })

    expect(fila[0]).toMatchObject({ ai: false, urgency: 'review' })
    expect(fila[0]!.title).not.toMatch(/sugest/i)
  })

  it('publicações a revisar entram depois dos compromissos, marcadas como da IA', () => {
    const fila = buildQueue({
      events: [evento('a', 5, 9)],
      processNumbers: vazio,
      reviews: [{ id: 'pub1', processNumber: '0001-00.2026.8.06.0001', source: 'TJCE' }],
      aiEnabled: true,
      now: NOW,
    })

    expect(fila.map((item) => item.id)).toEqual(['evento-a', 'revisao-pub1'])
    expect(fila[1]).toMatchObject({
      ai: true,
      urgency: 'review',
      due: null,
      href: '/publicacoes/pub1',
    })
  })
})

describe('filterQueue', () => {
  const fila = buildQueue({
    events: [evento('hoje', 0, 9), evento('amanha', 1, 9)],
    processNumbers: vazio,
    reviews: [{ id: 'p', processNumber: null, source: 'TJCE' }],
    aiEnabled: true,
    now: NOW,
  })

  it('"Hoje e atrasados" traz só o que vence hoje', () => {
    expect(filterQueue(fila, 'today').map((i) => i.id)).toEqual(['evento-hoje'])
  })

  it('"Sugeridos pela IA" traz só as revisões', () => {
    expect(filterQueue(fila, 'ai').map((i) => i.id)).toEqual(['revisao-p'])
  })

  it('"Todos" devolve tudo, sem alterar a lista original', () => {
    const copia = filterQueue(fila, 'all')

    expect(copia).toHaveLength(3)
    expect(copia).not.toBe(fila)
  })
})

describe('prazos', () => {
  const eventos = [
    evento('p-hoje-tarde', 0, 18),
    evento('p-hoje-cedo', 0, 9),
    evento('audiencia', 0, 14, { tipo: 'Audiencia' }),
    evento('p-amanha', 1, 9),
    evento('p-longe', 20, 9),
  ]

  it('prazosDoDia: só tipo Prazo, só hoje, em ordem de horário', () => {
    expect(prazosDoDia(eventos, NOW).map((e) => e.id)).toEqual(['p-hoje-cedo', 'p-hoje-tarde'])
  })

  it('prazosDaSemana: só Prazo, dentro da janela', () => {
    expect(prazosDaSemana(eventos, NOW).map((e) => e.id)).toEqual([
      'p-hoje-cedo',
      'p-hoje-tarde',
      'p-amanha',
    ])
  })
})
