import type { AlertaTab, AlertasFilters, AlertasSummary } from './types'

export const tabLabels: Record<AlertaTab, string> = {
  important: 'Importantes',
  publications: 'Publicações',
  deadlines: 'Prazos',
  all: 'Todos',
}

/**
 * Cada aba é um preset de filtro sobre a MESMA lista:
 *  - Importantes: tudo que ainda não foi lido (o que pede atenção agora);
 *  - Publicações: todos os alertas de publicação, lidos ou não (os não lidos já estão em
 *    Importantes);
 *  - Prazos: alertas de tarefa/prazo. A API não tem um tipo "prazo" próprio — `Task` é o que
 *    leva avisos como "Tarefa vence amanhã" (decisão do v4 em docs/frontend/v4-alinhamento.md);
 *  - Todos: sem restrição (aniversários e "outros" só aparecem aqui).
 */
export function tabFilters(tab: AlertaTab): Pick<AlertasFilters, 'types' | 'isRead'> {
  switch (tab) {
    case 'important':
      return { isRead: false }
    case 'publications':
      return { types: ['Publication'] }
    case 'deadlines':
      return { types: ['Task'] }
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
    case 'publications':
      return summary.totalByType.Publication ?? 0
    case 'deadlines':
      return summary.totalByType.Task ?? 0
    default:
      return summary.total
  }
}
