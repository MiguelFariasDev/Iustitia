import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { getPendingCounts } from './api'

/**
 * Contadores da navegação: leves e revalidados com frequência, já que a barra lateral
 * fica visível em todas as telas.
 */
export const pendingCountsQuery = (client: Parameters<typeof getPendingCounts>[0]) =>
  queryOptions({
    queryKey: queryKeys.navCounts,
    queryFn: () => getPendingCounts(client),
    staleTime: 60_000,
  })
