import { queryOptions } from '@tanstack/react-query'
import type { AxiosInstance } from 'axios'
import { queryKeys } from '@/lib/query/queryKeys'
import { getClienteById, listClientes } from './api'
import type { ClientesFilters } from './types'

/**
 * A chave inclui os filtros normalizados para que cada combinação tenha sua própria entrada
 * de cache — sem isso, trocar de filtro devolveria a lista anterior até a nova chegar.
 */
export const clientesQuery = (filters: ClientesFilters = {}, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.clientes.list(normalizeFilters(filters)),
    queryFn: () => listClientes(filters, client),
  })

export const clienteQuery = (id: string, client?: AxiosInstance) =>
  queryOptions({
    queryKey: queryKeys.clientes.byId(id),
    queryFn: () => getClienteById(id, client),
  })

/** Normaliza para a chave não mudar por diferença irrelevante (undefined vs null vs ''). */
function normalizeFilters(filters: ClientesFilters) {
  return {
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? 20,
    tipo: filters.tipo ?? null,
    apenasAtivos: filters.apenasAtivos ?? null,
    busca: filters.busca?.trim() || null,
  }
}
