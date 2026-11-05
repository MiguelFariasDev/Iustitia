import { queryOptions } from '@tanstack/react-query'
import type { AxiosInstance } from 'axios'
import { queryKeys } from '@/lib/query/queryKeys'
import { getProcessoById, listProcessos } from './api'
import type { ProcessosFilters } from './types'

export const processosQuery = (filters: ProcessosFilters = {}, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.processos.list(normalizeFilters(filters)),
    queryFn: () => listProcessos(filters, client),
  })

export const processoQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.processos.byId(id),
    queryFn: () => getProcessoById(id, client),
  })

/** Normaliza para a chave não mudar por diferença irrelevante (undefined vs null vs ''). */
export function normalizeFilters(filters: ProcessosFilters) {
  return {
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? 20,
    clienteId: filters.clienteId ?? null,
    area: filters.area ?? null,
    status: filters.status ?? null,
    busca: filters.busca?.trim() || null,
    // Estes quatro também vão para o servidor (ver listProcessos): fora da chave, mudar o
    // filtro devolveria a entrada de cache anterior em vez de buscar de novo.
    responsavelUserId: filters.responsavelUserId ?? null,
    tribunal: filters.tribunal?.trim() || null,
    distribuicaoDe: filters.distribuicaoDe || null,
    distribuicaoAte: filters.distribuicaoAte || null,
  }
}
