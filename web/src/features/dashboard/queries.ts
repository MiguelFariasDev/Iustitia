import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { getDashboardSummary } from './api'

export const dashboardSummaryQuery = (
  client: Parameters<typeof getDashboardSummary>[0] | undefined
) =>
  queryOptions({
    queryKey: queryKeys.dashboard.summary(),
    queryFn: () => getDashboardSummary(client),
  })
