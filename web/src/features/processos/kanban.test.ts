import { colunasKanban, findCard, isColunaKanban, moveCardNaPagina } from './kanban'
import type { ProcessoListItem, ProcessosPage } from './types'

const card = (id: string, status: ProcessoListItem['status'] = 'Ativo'): ProcessoListItem => ({
  id,
  cnjNumber: `000000${id}-00.2026.8.06.0001`,
  clienteId: 'c1',
  area: 'Civel',
  status,
  tribunal: 'TJCE',
  parteContraria: null,
  dataDistribuicao: null,
})

const page = (items: ProcessoListItem[], totalCount = items.length): ProcessosPage => ({
  items,
  page: 1,
  pageSize: 100,
  totalCount,
  totalPages: 1,
})

describe('colunas', () => {
  it('não inclui Arquivado: arquivar tem confirmação própria', () => {
    expect(colunasKanban).not.toContain('Arquivado')
    expect(isColunaKanban('Arquivado')).toBe(false)
    expect(isColunaKanban('Suspenso')).toBe(true)
  })
})

describe('moveCardNaPagina', () => {
  const a = card('1')

  it('remove o cartão da coluna de origem e ajusta o total', () => {
    const origem = moveCardNaPagina(page([a, card('2')]), 'Ativo', a, 'Suspenso')

    expect(origem.items.map((i) => i.id)).toEqual(['2'])
    expect(origem.totalCount).toBe(1)
  })

  it('insere no topo da coluna de destino com a situação nova', () => {
    const destino = moveCardNaPagina(page([card('9', 'Suspenso')]), 'Suspenso', a, 'Suspenso')

    expect(destino.items.map((i) => i.id)).toEqual(['1', '9'])
    expect(destino.items[0]!.status).toBe('Suspenso')
    expect(destino.totalCount).toBe(2)
  })

  it('não duplica se o cartão já está no destino', () => {
    const p = page([card('1', 'Suspenso')])

    expect(moveCardNaPagina(p, 'Suspenso', a, 'Suspenso')).toBe(p)
  })

  it('numa lista sem filtro de situação o cartão fica e só troca de situação', () => {
    const lista = moveCardNaPagina(page([a, card('2')]), null, a, 'Encerrado')

    expect(lista.items).toHaveLength(2)
    expect(lista.items[0]!.status).toBe('Encerrado')
    expect(lista.totalCount).toBe(2)
  })

  it('ignora páginas de outras colunas que não contêm o cartão', () => {
    const outra = page([card('7', 'Encerrado')])

    expect(moveCardNaPagina(outra, 'Encerrado', a, 'Suspenso')).toBe(outra)
  })

  it('nunca deixa o total negativo', () => {
    expect(moveCardNaPagina(page([a], 0), 'Ativo', a, 'Suspenso').totalCount).toBe(0)
  })
})

describe('findCard', () => {
  it('acha o cartão em qualquer página', () => {
    expect(findCard([page([card('1')]), page([card('2')])], '2')?.id).toBe('2')
    expect(findCard([page([card('1')])], 'x')).toBeUndefined()
  })
})
