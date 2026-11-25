import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { listEventos, listEventosDoProcesso } from './api'
import type { AgendaPeriodo } from './types'

export const agendaQuery = (periodo: AgendaPeriodo, responsavelId: string | null = null) =>
  queryOptions({
    queryKey: queryKeys.agenda.range(periodo.from, periodo.to, responsavelId),
    queryFn: () => listEventos(periodo, responsavelId),
  })

/** Sob o prefixo da agenda: criar, editar ou excluir um evento também atualiza a tela do processo. */
export const eventosDoProcessoQuery = (processoId: string) =>
  queryOptions({
    queryKey: [...queryKeys.agenda.all, 'processo', processoId] as const,
    queryFn: () => listEventosDoProcesso(processoId),
  })
