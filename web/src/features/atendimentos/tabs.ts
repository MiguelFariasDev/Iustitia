import type { AtendimentoTab, AtendimentosFilters } from './types'

export const tabLabels: Record<AtendimentoTab, string> = {
  all: 'Todos',
  pending: 'Aguardando',
  human: 'Humano',
  bot: 'Bot',
  closed: 'Encerrados',
}

/** Cada aba é um preset de status; "Todos" não filtra. */
export function tabFilters(tab: AtendimentoTab): Pick<AtendimentosFilters, 'status'> {
  switch (tab) {
    case 'pending':
      return { status: 'PendingHuman' }
    case 'human':
      return { status: 'Human' }
    case 'bot':
      return { status: 'Bot' }
    case 'closed':
      return { status: 'Closed' }
    default:
      return { status: null }
  }
}
