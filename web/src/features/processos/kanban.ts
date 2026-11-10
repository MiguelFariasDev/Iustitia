import type { ProcessoListItem, ProcessosPage, StatusProcesso } from './types'

/**
 * Colunas do quadro. "Arquivado" fica de fora de propósito: arquivar é um estado final com
 * endpoint e confirmação próprios (ver ProcessoDetail), e soltar um cartão por engano numa
 * coluna "Arquivado" encerraria a captura de publicações sem volta.
 */
export const colunasKanban: readonly StatusProcesso[] = ['Ativo', 'Suspenso', 'Encerrado']

/** Teto do servidor por página; cada coluna mostra até isto e avisa quando há mais. */
export const KANBAN_PAGE_SIZE = 100

export function isColunaKanban(status: string): status is StatusProcesso {
  return (colunasKanban as readonly string[]).includes(status)
}

export function findCard(
  pages: readonly ProcessosPage[],
  id: string
): ProcessoListItem | undefined {
  for (const page of pages) {
    const found = page.items.find((item) => item.id === id)
    if (found) return found
  }

  return undefined
}

/**
 * Aplica a mudança de coluna numa página já em cache. `pageStatus` é o filtro de situação
 * com que a página foi buscada: quem a mostra é a coluna daquele status.
 *
 * - página da coluna de origem (ou qualquer uma que contenha o cartão sem ser a de destino):
 *   o remove;
 * - página da coluna de destino: o insere no topo, com a situação nova.
 *
 * Funções puras, sem tocar o QueryClient, para dar para testar sem React.
 */
export function moveCardNaPagina(
  page: ProcessosPage,
  pageStatus: StatusProcesso | null,
  card: ProcessoListItem,
  destino: StatusProcesso
): ProcessosPage {
  const contem = page.items.some((item) => item.id === card.id)

  if (pageStatus === destino) {
    if (contem) return page
    const movido: ProcessoListItem = { ...card, status: destino }

    return { ...page, items: [movido, ...page.items], totalCount: page.totalCount + 1 }
  }

  if (!contem) return page

  if (pageStatus === null) {
    // Lista sem filtro de situação: o cartão continua nela, só muda de situação.
    return {
      ...page,
      items: page.items.map((item) => (item.id === card.id ? { ...item, status: destino } : item)),
    }
  }

  return {
    ...page,
    items: page.items.filter((item) => item.id !== card.id),
    totalCount: Math.max(0, page.totalCount - 1),
  }
}
