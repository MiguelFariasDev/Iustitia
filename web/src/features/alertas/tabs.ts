import type { AlertaTab, AlertasFilters, AlertasSummary } from './types'

export const tabLabels: Record<AlertaTab, string> = {
  important: 'Importantes',
  all: 'Todos',
  publications: 'Publicações não tratadas',
  others: 'Outros',
}

/**
 * Cada aba é um preset de filtro sobre a MESMA lista:
 *  - Importantes: tudo que ainda não foi lido (o que pede atenção agora);
 *  - Todos: sem restrição;
 *  - Publicações não tratadas: alertas de publicação ainda não lidos;
 *  - Outros: tudo que não é publicação (tarefas, aniversários…).
 */
export function tabFilters(tab: AlertaTab): Pick<AlertasFilters, 'types' | 'isRead'> {
  switch (tab) {
    case 'important':
      return { isRead: false }
    case 'publications':
      return { types: ['Publication'], isRead: false }
    case 'others':
      return { types: ['Task', 'Birthday', 'Other'] }
    default:
      return {}
  }
}

/** Contagem de cada aba, a partir do resumo (uma consulta só para as quatro). */
export function tabCount(tab: AlertaTab, summary: AlertasSummary | undefined): number | null {
  if (!summary) return null

  switch (tab) {
    case 'important':
      return summary.unread
    case 'all':
      return summary.total
    case 'publications':
      return summary.unreadByType.Publication ?? 0
    default:
      return summary.total - (summary.totalByType.Publication ?? 0)
  }
}
