import { countActiveFilters, filtersFromSearchParams, filtersToSearchParams } from './filters'

const parse = (query: string) => filtersFromSearchParams(new URLSearchParams(query))

describe('filtros da tela de publicações na URL', () => {
  it('sem parâmetros, não há filtro e a página é a 1', () => {
    expect(parse('')).toMatchObject({
      page: 1,
      status: null,
      isRead: null,
      busca: null,
      tribunal: null,
      from: null,
      to: null,
    })
  })

  it('lê todos os filtros conhecidos (links do painel e de outras telas)', () => {
    expect(
      parse(
        'status=treated&isRead=false&tribunal=TJSP&clienteId=c1&processId=p1&dataInicio=2026-09-01&dataFim=2026-09-30&busca=silva&page=3&ordem=oldest'
      )
    ).toEqual({
      page: 3,
      status: 'treated',
      isRead: false,
      tribunal: 'TJSP',
      clienteId: 'c1',
      processId: 'p1',
      from: '2026-09-01',
      to: '2026-09-30',
      busca: 'silva',
      ordem: 'oldest',
    })
  })

  it.each([
    ['status=inexistente', 'status'],
    ['dataInicio=ontem', 'from'],
    ['dataFim=31/12/2026', 'to'],
    ['isRead=talvez', 'isRead'],
    ['ordem=aleatoria', 'ordem'],
  ])('IGNORA o valor inválido (%s) em vez de transformá-lo em filtro', (query, key) => {
    // Uma URL antiga ou editada à mão nunca deve esconder publicações.
    expect(parse(query)[key as keyof ReturnType<typeof parse>]).toBeNull()
  })

  it.each(['0', '-2', 'abc', '1.5'])('página inválida (%s) cai na primeira', (page) => {
    expect(parse(`page=${page}`).page).toBe(1)
  })

  it('serializa só o que foi definido — a URL padrão fica limpa', () => {
    expect(filtersToSearchParams({ page: 1, ordem: 'recent' }).toString()).toBe('')
    expect(
      filtersToSearchParams({ page: 2, status: 'untreated', isRead: false, busca: 'x' }).toString()
    ).toBe('page=2&status=untreated&isRead=false&busca=x')
  })

  it('ida e volta preserva os filtros', () => {
    const filters = parse('status=archived&tribunal=TRF3&dataInicio=2026-01-01&ordem=tribunal-desc')

    expect(parse(filtersToSearchParams(filters).toString())).toEqual(filters)
  })

  it('conta os filtros ativos, sem contar paginação nem ordenação', () => {
    expect(countActiveFilters({ page: 4, ordem: 'oldest' })).toBe(0)
    expect(countActiveFilters({ status: 'treated', isRead: false, busca: 'x' })).toBe(3)
    // isRead=false é um filtro ativo, não "ausente".
    expect(countActiveFilters({ isRead: false })).toBe(1)
  })
})
