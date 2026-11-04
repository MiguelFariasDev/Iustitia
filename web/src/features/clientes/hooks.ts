'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { routes } from '@/config/routes'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { createCliente, deactivateCliente, reactivateCliente, updateCliente } from './api'
import type { ClienteFormValues } from './schemas'

/**
 * Mutations de cliente.
 *
 * Invalidam por PREFIXO (`queryKeys.clientes.all`) em vez de por chave exata: a listagem tem
 * uma entrada de cache por combinação de filtros, e invalidar só a atual deixaria as outras
 * mostrando dado velho assim que o usuário trocasse o filtro.
 *
 * O erro é normalizado por handleApiError, que decide se merece toast — erro de formulário
 * pertence ao campo, não a um toast que tira a atenção do input errado.
 */
export function useCreateCliente() {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ClienteFormValues) => createCliente(values),
    onSuccess: (cliente) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.clientes.all })
      notify.success('Cliente cadastrado.', { description: cliente.nome })
      router.push(`${routes.clients}/${cliente.id}`)
    },
    onError: (error) => handleApiError(error),
  })
}

export function useUpdateCliente(id: string) {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ClienteFormValues) => updateCliente(id, values),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.clientes.all })
      notify.success('Cliente atualizado.')
      router.push(`${routes.clients}/${id}`)
    },
    onError: (error) => handleApiError(error),
  })
}

export function useDeactivateCliente() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deactivateCliente(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.clientes.all })
      // "Desativado", não "excluído": o histórico e os processos do cliente permanecem.
      notify.success('Cliente desativado.', {
        description: 'O histórico e os processos dele continuam disponíveis.',
      })
    },
    onError: (error) => handleApiError(error),
  })
}

export function useReactivateCliente() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => reactivateCliente(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.clientes.all })
      notify.success('Cliente reativado.')
    },
    onError: (error) => handleApiError(error),
  })
}
