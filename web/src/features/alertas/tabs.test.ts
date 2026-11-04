import { tabCount, tabFilters } from './tabs'
import type { AlertasSummary } from './types'

const summary: AlertasSummary = {
  total: 10,
  unread: 4,
  unreadByType: { Publication: 3, Task: 1 },
  totalByType: { Publication: 6, Task: 3, Birthday: 1 },
}

describe('abas de alertas', () => {
  it('cada aba é um preset de filtro', () => {
    expect(tabFilters('important')).toEqual({ isRead: false })
    expect(tabFilters('all')).toEqual({})
    expect(tabFilters('publications')).toEqual({ types: ['Publication'], isRead: false })
    expect(tabFilters('others')).toEqual({ types: ['Task', 'Birthday', 'Other'] })
  })

  it('as contagens saem de UM resumo', () => {
    expect(tabCount('important', summary)).toBe(4)
    expect(tabCount('all', summary)).toBe(10)
    expect(tabCount('publications', summary)).toBe(3)
    // "Outros" = tudo que não é publicação, lidos incluídos.
    expect(tabCount('others', summary)).toBe(4)
  })

  it('sem resumo ainda, não inventa contagem', () => {
    expect(tabCount('all', undefined)).toBeNull()
  })

  it('"Publicações" é zero quando não há alerta de publicação não lido', () => {
    expect(tabCount('publications', { ...summary, unreadByType: {} })).toBe(0)
  })
})
