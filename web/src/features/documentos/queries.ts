import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { listDocumentos } from './api'
import type { DocumentosFilters } from './types'

export const documentosQuery = (filters: DocumentosFilters = {}) =>
  queryOptions({
    queryKey: queryKeys.documentos.list({
      busca: filters.busca?.trim() || null,
      tipoArquivo: filters.tipoArquivo ?? null,
      vinculo: filters.vinculo ?? null,
    }),
    queryFn: () => listDocumentos(filters),
  })
