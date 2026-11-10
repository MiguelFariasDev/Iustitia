import {
  CIRCUNFERENCIA,
  colunaVizinha,
  deslocamento,
  moverTarefa,
  progresso,
  rotuloColuna,
  type Tarefa,
} from './kanban'

const AGORA = new Date(2026, 10, 5, 14, 0) // quinta, 5/nov/2026

const tarefa = (extra: Partial<Tarefa> = {}): Tarefa => ({
  id: 't1',
  titulo: 'Contestação',
  processo: '0000777-07.2018.8.06.0100',
  responsavel: 'Marina Lopes',
  coluna: 'andamento',
  prazo: '2026-11-15',
  criadaEm: new Date(2026, 10, 1).toISOString(), // 1/nov
  ...extra,
})

describe('colunas', () => {
  it('vizinhas: esquerda e direita, e null nas pontas', () => {
    expect(colunaVizinha('andamento', -1)).toBe('afazer')
    expect(colunaVizinha('andamento', 1)).toBe('revisao')
    expect(colunaVizinha('afazer', -1)).toBeNull()
    expect(colunaVizinha('concluido', 1)).toBeNull()
  })

  it('rótulos', () => {
    expect(rotuloColuna('revisao')).toBe('Em revisão')
  })
})

describe('moverTarefa', () => {
  it('muda só a coluna da tarefa pedida, sem alterar a lista original', () => {
    const lista = [tarefa(), tarefa({ id: 't2' })]
    const nova = moverTarefa(lista, 't1', 'revisao')

    expect(nova.map((t) => t.coluna)).toEqual(['revisao', 'andamento'])
    expect(lista[0]!.coluna).toBe('andamento')
  })

  it('id inexistente não altera nada', () => {
    const lista = [tarefa()]

    expect(moverTarefa(lista, 'nao-existe', 'revisao')).toEqual(lista)
  })
})

describe('progresso do prazo', () => {
  it('% decorrido entre a criação e o prazo', () => {
    // 1/nov → 15/nov = 14 dias; hoje (5/nov) = 4 dias → 29%
    const p = progresso(tarefa(), AGORA)

    expect(p.pct).toBe(29)
    expect(p.tom).toBe('ok')
    expect(p.rotulo).toBe('1s') // 10 dias restantes = 1 semana
  })

  it('rótulo: dias abaixo de uma semana, semanas a partir de 7 dias', () => {
    expect(progresso(tarefa({ prazo: '2026-11-07' }), AGORA).rotulo).toBe('2d')
    expect(progresso(tarefa({ prazo: '2026-11-05' }), AGORA).rotulo).toBe('0d')
    expect(progresso(tarefa({ prazo: '2026-11-12' }), AGORA).rotulo).toBe('1s')
    expect(progresso(tarefa({ prazo: '2026-11-26' }), AGORA).rotulo).toBe('3s')
  })

  it('cor: violeta a partir de 70%, vermelho a partir de 90%', () => {
    // criada 1/nov; prazo 7/nov = 6 dias; hoje = 4 dias → 67% (verde)
    expect(progresso(tarefa({ prazo: '2026-11-07' }), AGORA).tom).toBe('ok')
    // prazo 6/nov = 5 dias; hoje = 4 → 80% (violeta)
    expect(progresso(tarefa({ prazo: '2026-11-06' }), AGORA).tom).toBe('atencao')
    // prazo 5/nov = 4 dias; hoje = 4 → 100% (vermelho)
    expect(progresso(tarefa({ prazo: '2026-11-05' }), AGORA)).toMatchObject({
      pct: 100,
      tom: 'critico',
    })
  })

  it('prazo vencido: 100%, vermelho, rótulo negativo', () => {
    expect(progresso(tarefa({ prazo: '2026-11-02' }), AGORA)).toEqual({
      pct: 100,
      tom: 'critico',
      rotulo: '-3d',
    })
  })

  it('concluída: "ok" e verde, qualquer que seja o prazo', () => {
    expect(progresso(tarefa({ coluna: 'concluido', prazo: '2026-11-01' }), AGORA)).toEqual({
      pct: 100,
      tom: 'ok',
      rotulo: 'ok',
    })
  })

  it('sem prazo: 0% e "—", sem alarme', () => {
    expect(progresso(tarefa({ prazo: null }), AGORA)).toEqual({ pct: 0, tom: 'ok', rotulo: '—' })
  })

  it('criada hoje com prazo hoje não divide por zero', () => {
    const p = progresso(tarefa({ prazo: '2026-11-05', criadaEm: AGORA.toISOString() }), AGORA)

    expect(Number.isFinite(p.pct)).toBe(true)
  })

  it('o prazo é um dia LOCAL: "2026-11-05" é 5/nov para quem está no Brasil, não 4/nov', () => {
    expect(progresso(tarefa({ prazo: '2026-11-05' }), new Date(2026, 10, 5, 0, 30)).rotulo).toBe(
      '0d'
    )
  })
})

describe('anel', () => {
  it('deslocamento = circunferência × (1 − pct/100)', () => {
    expect(CIRCUNFERENCIA).toBe(94.2)
    expect(deslocamento(0)).toBeCloseTo(94.2)
    expect(deslocamento(100)).toBeCloseTo(0)
    expect(deslocamento(50)).toBeCloseTo(47.1)
  })
})
