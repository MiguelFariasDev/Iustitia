'use client'

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { routes } from '@/config/routes'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { archiveProcesso, changeProcessoStatus, createProcesso, updateProcesso } from './api'
import { consultarProcessoTribunal } from './datajud'
import type { ProcessoFormValues } from './schemas'
import { statusProcessoMeta, type StatusProcesso } from './types'

export function useCreateProcesso() {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ProcessoFormValues) => createProcesso(values),
    onSuccess: (processo) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.processos.all })
      // O aviso diz o que passa a acontecer: cadastrar um processo liga a captura dele.
      notify.success('Processo cadastrado.', {
        description: `A partir de agora as publicações de ${processo.cnjNumber} serão capturadas.`,
      })
      router.push(`${routes.processes}/${processo.id}` as never)
    },
    onError: (error) => handleApiError(error),
  })
}

/**
 * Consulta os metadados do processo no tribunal para auto-preencher o cadastro.
 *
 * É mutation, e não query, apesar de só ler: a consulta é DISPARADA pela pessoa, uma por
 * vez, e conta contra um limite de 10/min. Como query, o TanStack a refaria em foco de
 * janela e remontagem — queimando o limite sem ninguém pedir.
 *
 * O cache de 1h vive no backend (Redis), onde é compartilhado por todo o escritório.
 */
export function useConsultarTribunal() {
  return useMutation({
    mutationFn: (cnjNumber: string) => consultarProcessoTribunal(cnjNumber),
    // Sem toast de erro aqui: o modal mostra a falha no próprio contexto, com o botão de
    // seguir manualmente ao lado. Um toast empilharia a mesma informação duas vezes.
  })
}

export function useUpdateProcesso(id: string, responsavelUserId: string) {
  const queryClient = useQueryClient()
  const router = useRouter()

  return useMutation({
    mutationFn: (values: ProcessoFormValues) => updateProcesso(id, values, responsavelUserId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.processos.all })
      notify.success('Processo atualizado.')
      router.push(`${routes.processes}/${id}` as never)
    },
    onError: (error) => handleApiError(error),
  })
}

export function useChangeProcessoStatus(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (status: StatusProcesso) => changeProcessoStatus(id, status),
    onSuccess: (_data, status) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.processos.all })
      notify.success(`Processo marcado como ${statusProcessoMeta[status].label.toLowerCase()}.`, {
        description: statusProcessoMeta[status].descricao,
      })
    },
    onError: (error) => handleApiError(error),
  })
}

export function useArchiveProcesso(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => archiveProcesso(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.processos.all })
      notify.success('Processo arquivado.', {
        description: 'As publicações deste processo deixam de ser capturadas.',
      })
    },
    onError: (error) => handleApiError(error),
  })
}
