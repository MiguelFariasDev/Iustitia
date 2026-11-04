import {
  publicationStatuses,
  type PublicationSort,
  type PublicationStatus,
  type PublicationsFilters,
} from './types'

const sorts: PublicationSort[] = ['recent', 'oldest', 'tribunal', 'tribunal-desc']
const isoDate = /^\d{4}-\d{2}-\d{2}$/

/**
 * A URL é a fonte de verdade dos filtros da tela: assim os cartões do painel e os links de
 * outras telas (ex.: "publicações deste processo") levam à lista JÁ FILTRADA, e a página
 * pode ser compartilhada/recarregada sem perder o estado. Valor desconhecido é ignorado, não
 * vira filtro vazio — uma URL antiga ou editada à mão nunca deve esconder publicações.
 */
export function filtersFromSearchParams(params: URLSearchParams): PublicationsFilters {
  const status = params.get('status') as PublicationStatus | null
  const isRead = params.get('isRead')
  const ordem = params.get('ordem') as PublicationSort | null
  const from = params.get('dataInicio')
  const to = params.get('dataFim')
  const page = Number(params.get('page'))

  return {
    page: Number.isInteger(page) && page > 0 ? page : 1,
    status: status && publicationStatuses.includes(status) ? status : null,
    tribunal: params.get('tribunal') || null,
    clienteId: params.get('clienteId') || null,
    processId: params.get('processId') || null,
    from: from && isoDate.test(from) ? from : null,
    to: to && isoDate.test(to) ? to : null,
    isRead: isRead === 'true' ? true : isRead === 'false' ? false : null,
    busca: params.get('busca') || null,
    ordem: ordem && sorts.includes(ordem) ? ordem : null,
  }
}

export function filtersToSearchParams(filters: PublicationsFilters): URLSearchParams {
  const params = new URLSearchParams()

  if (filters.page && filters.page > 1) params.set('page', String(filters.page))
  if (filters.status) params.set('status', filters.status)
  if (filters.tribunal) params.set('tribunal', filters.tribunal)
  if (filters.clienteId) params.set('clienteId', filters.clienteId)
  if (filters.processId) params.set('processId', filters.processId)
  if (filters.from) params.set('dataInicio', filters.from)
  if (filters.to) params.set('dataFim', filters.to)
  if (filters.isRead !== null && filters.isRead !== undefined) {
    params.set('isRead', String(filters.isRead))
  }
  if (filters.busca) params.set('busca', filters.busca)
  if (filters.ordem && filters.ordem !== 'recent') params.set('ordem', filters.ordem)

  return params
}

/** Quantos filtros além da paginação/ordenação estão ativos (para o "Limpar filtros"). */
export function countActiveFilters(filters: PublicationsFilters): number {
  return [
    filters.status,
    filters.tribunal,
    filters.clienteId,
    filters.processId,
    filters.from,
    filters.to,
    filters.isRead,
    filters.busca,
  ].filter((value) => value !== null && value !== undefined && value !== '').length
}
