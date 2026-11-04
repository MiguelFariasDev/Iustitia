import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { getContatoById, listContatos } from './api'
import type { ContatosFilters } from './types'

/** Filtros normalizados na chave: `undefined` e `null` são a mesma consulta. */
export const contatosQuery = (filters: ContatosFilters = {}) =>
  queryOptions({
    queryKey: queryKeys.contatos.list({ tipo: filters.tipo ?? null }),
    queryFn: () => listContatos(filters),
  })

export const contatoQuery = (id: string) =>
  queryOptions({
    queryKey: queryKeys.contatos.byId(id),
    queryFn: () => getContatoById(id),
  })
