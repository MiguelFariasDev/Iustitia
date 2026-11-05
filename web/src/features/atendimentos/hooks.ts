'use client'

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { changeStatus, linkCliente, sendMensagem } from './api'
import { atendimentoQuery, atendimentosQuery, mensagensQuery } from './queries'
import type { AtendimentosFilters, StatusAlvo } from './types'

export function useAtendimentos(filters: AtendimentosFilters) {
  return useQuery({ ...atendimentosQuery(filters), placeholderData: keepPreviousData })
}

export function useAtendimento(id: string | null) {
  return useQuery({ ...atendimentoQuery(id ?? ''), enabled: Boolean(id) })
}

export function useMensagens(id: string | null) {
  return useQuery({ ...mensagensQuery(id ?? ''), enabled: Boolean(id) })
}

/** Fila, detalhe e mensagens dependem uns dos outros: invalida o prefixo todo. */
function useInvalidateAtendimentos() {
  const queryClient = useQueryClient()

  return () => queryClient.invalidateQueries({ queryKey: queryKeys.atendimentos.all })
}

const statusSuccess: Record<StatusAlvo, string> = {
  Human: 'Você assumiu o atendimento. O bot não responde mais nesta conversa.',
  Bot: 'Atendimento devolvido ao bot.',
  Closed: 'Atendimento encerrado.',
}

export function useChangeAtendimentoStatus(id: string) {
  const invalidate = useInvalidateAtendimentos()

  return useMutation({
    mutationFn: (status: StatusAlvo) => changeStatus(id, status),
    onSuccess: (_, status) => {
      notify.success(statusSuccess[status])
      return invalidate()
    },
    // Conflito (outro advogado assumiu, já encerrado…) deixa a tela desatualizada: recarrega.
    onError: (error) => {
      handleApiError(error)
      void invalidate()
    },
  })
}

export function useSendMensagem(id: string) {
  const invalidate = useInvalidateAtendimentos()

  return useMutation({
    mutationFn: (body: string) => sendMensagem(id, body),
    onSuccess: () => invalidate(),
    // Mesmo na falha de entrega a mensagem foi gravada (como "Falhou"): recarrega para mostrá-la.
    onError: (error) => {
      handleApiError(error)
      void invalidate()
    },
  })
}

export function useLinkCliente(id: string) {
  const invalidate = useInvalidateAtendimentos()

  return useMutation({
    mutationFn: (clienteId: string) => linkCliente(id, clienteId),
    onSuccess: () => {
      notify.success('Conversa vinculada ao cliente.')
      return invalidate()
    },
    onError: (error) => handleApiError(error),
  })
}
