import type { Publication } from '@/features/publicacoes/types'
import { contarAguardandoIa, montarFila, proximoNaoDecidido, type Resultado } from './fila'

const pub = (id: string, extra: Partial<Publication> = {}): Publication =>
  ({
    id,
    publishedAt: '2026-11-05T09:00:00Z',
    kind: 'citation',
    processId: 'p-' + id,
    content: 'Texto.',
    ...extra,
  }) as Publication

const feito: Resultado = { decisao: 'accepted', dias: 5, vencimento: '2026-11-12', sugeridoDias: 5 }

describe('montarFila', () => {
  it('só entra publicação com processo vinculado: sem processo não há prazo a salvar na agenda', () => {
    const fila = montarFila([pub('a'), pub('b', { processId: null })], true)

    expect(fila.map((i) => i.publication.id)).toEqual(['a'])
  })

  it('mais recentes primeiro', () => {
    const fila = montarFila(
      [
        pub('antiga', { publishedAt: '2026-11-01T09:00:00Z' }),
        pub('nova', { publishedAt: '2026-11-05T09:00:00Z' }),
      ],
      true
    )

    expect(fila.map((i) => i.publication.id)).toEqual(['nova', 'antiga'])
  })

  it('cada item traz a sugestão de demonstração', () => {
    expect(montarFila([pub('a')], true)[0]!.sugestao.tipo).toBe('Contestação')
  })
})

describe('montarFila com a API real', () => {
  const sugestao = {
    actType: 'Contestação',
    deadlineDays: 15,
    deadlineKind: 'business' as const,
    legalBasis: 'CPC, art. 335',
    confidence: 91,
    requiresManualReview: false,
    contentTruncated: false,
  }

  it('usa a sugestão DA IA (e não a de demonstração) e só entra quem foi analisado', () => {
    const fila = montarFila([pub('analisada', { suggestion: sugestao }), pub('pendente-de-ia')])

    expect(fila.map((i) => i.publication.id)).toEqual(['analisada'])
    expect(fila[0]!.sugestao).toMatchObject({
      tipo: 'Contestação',
      dias: 15,
      fundamento: 'CPC, art. 335',
      confianca: 91,
      corridos: false,
      semPrazo: false,
    })
  })

  it('publicação sem processo vinculado também pode ser revisada (o servidor agenda sem processo)', () => {
    expect(montarFila([pub('a', { processId: null, suggestion: sugestao })])).toHaveLength(1)
  })

  it('traduz prazo em dias corridos, ausência de prazo e baixa confiança', () => {
    const [corrido, semPrazo] = montarFila([
      pub('c', {
        publishedAt: '2026-11-06T09:00:00Z',
        suggestion: { ...sugestao, deadlineKind: 'calendar' },
      }),
      pub('s', {
        suggestion: {
          ...sugestao,
          deadlineDays: null,
          deadlineKind: null,
          requiresManualReview: true,
        },
      }),
    ])

    expect(corrido!.sugestao.corridos).toBe(true)
    expect(semPrazo!.sugestao).toMatchObject({ semPrazo: true, dias: 0, revisaoManual: true })
  })

  it('conta as publicações que a IA ainda não analisou', () => {
    expect(
      contarAguardandoIa([
        pub('a', { suggestion: sugestao }),
        pub('b'),
        pub('c', { suggestion: null }),
      ])
    ).toBe(2)
  })
})

describe('proximoNaoDecidido', () => {
  const itens = montarFila(
    [
      pub('1', { publishedAt: '2026-11-05T12:00:00Z' }),
      pub('2', { publishedAt: '2026-11-04T12:00:00Z' }),
      pub('3', { publishedAt: '2026-11-03T12:00:00Z' }),
    ],
    true
  )

  it('vai à próxima ainda não decidida DEPOIS da atual', () => {
    expect(proximoNaoDecidido(itens, { '1': feito }, '1')).toBe('2')
  })

  it('pula as já decididas', () => {
    expect(proximoNaoDecidido(itens, { '1': feito, '2': feito }, '1')).toBe('3')
  })

  it('no fim da fila, volta à primeira ainda pendente', () => {
    expect(proximoNaoDecidido(itens, { '2': feito, '3': feito }, '3')).toBe('1')
  })

  it('tudo decidido: fica onde está', () => {
    expect(proximoNaoDecidido(itens, { '1': feito, '2': feito, '3': feito }, '2')).toBe('2')
  })
})
