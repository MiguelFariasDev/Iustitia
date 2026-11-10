import type { Publication } from '@/features/publicacoes/types'
import { montarFila, proximoNaoDecidido, type Resultado } from './fila'

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
    const fila = montarFila([pub('a'), pub('b', { processId: null })])

    expect(fila.map((i) => i.publication.id)).toEqual(['a'])
  })

  it('mais recentes primeiro', () => {
    const fila = montarFila([
      pub('antiga', { publishedAt: '2026-11-01T09:00:00Z' }),
      pub('nova', { publishedAt: '2026-11-05T09:00:00Z' }),
    ])

    expect(fila.map((i) => i.publication.id)).toEqual(['nova', 'antiga'])
  })

  it('cada item traz a sugestão de demonstração', () => {
    expect(montarFila([pub('a')])[0]!.sugestao.tipo).toBe('Contestação')
  })
})

describe('proximoNaoDecidido', () => {
  const itens = montarFila([
    pub('1', { publishedAt: '2026-11-05T12:00:00Z' }),
    pub('2', { publishedAt: '2026-11-04T12:00:00Z' }),
    pub('3', { publishedAt: '2026-11-03T12:00:00Z' }),
  ])

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
