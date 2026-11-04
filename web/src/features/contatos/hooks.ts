'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { routes } from '@/config/routes'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { createContato, deleteContato, updateContato } from './api'
import { contatoQuery, contatosQuery } from './queries'
import type { ContatoFormValues } from './schemas'
import type { ContatosFilters } from './types'

/**
 * Hooks de contatos (list, byId, create, update, delete). Num arquivo só, como nas demais
 * features do projeto, em vez de um arquivo por hook.
 *
 * As mutations invalidam por PREFIXO (`queryKeys.contatos.all`): a lista tem uma entrada de
 * cache por filtro, e invalidar só a atual deixaria as outras com dado velho.
 */
export function useContatos(filters: ContatosFilters = {}) {
  return useQuery({ ...contatosQuery(filters), placeholderData: (previous) => previous })
}

export function useContato(id: string) {
  return useQuery(contatoQuery(id))
}

export function useCreateContato() {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ContatoFormValues) => createContato(values),
    onSuccess: (contato) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.contatos.all })
      notify.success('Contato cadastrado.', { description: contato.nome })
      router.push(routes.contacts)
    },
    onError: (error) => handleApiError(error),
  })
}

export function useUpdateContato(id: string) {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ContatoFormValues) => updateContato(id, values),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.contatos.all })
      notify.success('Contato atualizado.')
      router.push(routes.contacts)
    },
    onError: (error) => handleApiError(error),
  })
}

export function useDeleteContato() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteContato(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.contatos.all })
      notify.success('Contato excluído.')
    },
    onError: (error) => handleApiError(error),
  })
}
