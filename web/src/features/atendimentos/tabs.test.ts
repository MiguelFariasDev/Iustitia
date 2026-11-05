import { tabFilters, tabLabels } from './tabs'
import { atendimentoTabs } from './types'

describe('tabFilters', () => {
  it('cada aba é um preset de status; "Todos" não filtra', () => {
    expect(tabFilters('all')).toEqual({ status: null })
    expect(tabFilters('pending')).toEqual({ status: 'PendingHuman' })
    expect(tabFilters('human')).toEqual({ status: 'Human' })
    expect(tabFilters('bot')).toEqual({ status: 'Bot' })
    expect(tabFilters('closed')).toEqual({ status: 'Closed' })
  })

  it('toda aba tem rótulo', () => {
    for (const tab of atendimentoTabs) expect(tabLabels[tab]).toBeTruthy()
  })
})
