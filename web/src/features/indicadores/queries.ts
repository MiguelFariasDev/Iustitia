import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { getIndicadores } from './api'
import type { Periodo } from './types'

export const indicadoresQuery = (periodo: Periodo) =>
  queryOptions({
    queryKey: queryKeys.indicadores.periodo(periodo),
    queryFn: () => getIndicadores(periodo),
  })
