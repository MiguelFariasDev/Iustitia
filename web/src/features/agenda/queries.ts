import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { listEventos } from './api'
import type { AgendaPeriodo } from './types'

export const agendaQuery = (periodo: AgendaPeriodo, responsavelId: string | null = null) =>
  queryOptions({
    queryKey: queryKeys.agenda.range(periodo.from, periodo.to, responsavelId),
    queryFn: () => listEventos(periodo, responsavelId),
  })
