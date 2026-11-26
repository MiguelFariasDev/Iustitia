'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { concluirEvento, createEvento, deleteEvento, rescheduleEvento, updateEvento } from './api'
import { agendaQuery } from './queries'
import type { EventoFormValues } from './schemas'
import type { AgendaEvento, AgendaPeriodo } from './types'

export function useAgenda(
  periodo: AgendaPeriodo,
  responsavelId: string | null = null,
  { enabled = true }: { enabled?: boolean } = {}
) {
  return useQuery({
    ...agendaQuery(periodo, responsavelId),
    placeholderData: (previous) => previous,
    enabled,
  })
}

/** Invalida por prefixo: o mesmo evento aparece em várias janelas em cache (mês, semana, dia). */
function useInvalidateAgenda() {
  const queryClient = useQueryClient()

  return () => queryClient.invalidateQueries({ queryKey: queryKeys.agenda.all })
}

export function useCreateEvento(onDone?: () => void) {
  const invalidate = useInvalidateAgenda()

  return useMutation({
    mutationFn: (values: EventoFormValues) => createEvento(values),
    onSuccess: (evento) => {
      void invalidate()
      notify.success('Evento criado.', { description: evento.titulo })
      onDone?.()
    },
    onError: (error) => handleApiError(error),
  })
}

export function useUpdateEvento(id: string, onDone?: () => void) {
  const invalidate = useInvalidateAgenda()

  return useMutation({
    mutationFn: (values: EventoFormValues) => updateEvento(id, values),
    onSuccess: () => {
      void invalidate()
      notify.success('Evento atualizado.')
      onDone?.()
    },
    onError: (error) => handleApiError(error),
  })
}

export function useDeleteEvento(onDone?: () => void) {
  const invalidate = useInvalidateAgenda()

  return useMutation({
    mutationFn: (id: string) => deleteEvento(id),
    onSuccess: () => {
      void invalidate()
      notify.success('Evento excluído.')
      onDone?.()
    },
    onError: (error) => handleApiError(error),
  })
}

/**
 * Concluir/reabrir um prazo, audiência etc. OTIMISTA: o item muda na hora em todas as janelas em cache
 * (agenda, painel, aba do processo) e volta se o servidor recusar.
 */
export function useConcluirEvento() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, concluido }: { id: string; concluido: boolean }) =>
      concluirEvento(id, concluido),
    onMutate: async ({ id, concluido }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.agenda.all })
      const snapshot = queryClient.getQueriesData<AgendaEvento[]>({
        queryKey: queryKeys.agenda.all,
      })
      const quando = concluido ? new Date().toISOString() : null

      queryClient.setQueriesData<AgendaEvento[]>({ queryKey: queryKeys.agenda.all }, (current) =>
        Array.isArray(current)
          ? current.map((evento) => (evento.id === id ? { ...evento, concluidoEm: quando } : evento))
          : current
      )

      return { snapshot }
    },
    onError: (error, _variables, context) => {
      context?.snapshot.forEach(([key, data]) => queryClient.setQueryData(key, data))
      handleApiError(error)
    },
    onSuccess: (_data, { concluido }) => {
      notify.success(concluido ? 'Marcado como concluído.' : 'Reaberto.')
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.agenda.all })
    },
  })
}

/**
 * Arrastar e soltar com atualização OTIMISTA: o evento muda de lugar na hora, nas janelas em
 * cache, e volta para onde estava se o servidor recusar. Sem isso o chip "pula de volta" por
 * 200–400 ms até a resposta chegar, o que parece que o arrasto falhou.
 */
export function useRescheduleEvento() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, inicio, fim }: { id: string; inicio: string; fim: string }) =>
      rescheduleEvento(id, { inicio, fim }),
    onMutate: async ({ id, inicio, fim }) => {
      await queryClient.cancelQueries({ queryKey: queryKeys.agenda.all })
      const snapshot = queryClient.getQueriesData<AgendaEvento[]>({
        queryKey: queryKeys.agenda.all,
      })

      queryClient.setQueriesData<AgendaEvento[]>({ queryKey: queryKeys.agenda.all }, (current) =>
        current?.map((evento) => (evento.id === id ? { ...evento, inicio, fim } : evento))
      )

      return { snapshot }
    },
    onError: (error, _variables, context) => {
      context?.snapshot.forEach(([key, data]) => queryClient.setQueryData(key, data))
      handleApiError(error)
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.agenda.all })
    },
  })
}
