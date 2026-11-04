'use client'

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api/errors'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { changePublicationStatus, markPublicationAsRead } from './api'
import type { PublicationApiStatus } from './contract'
import { publicationQuery, publicationsQuery, publicationsSummaryQuery } from './queries'
import type { PublicationsFilters } from './types'

/** Listagem filtrada. `keepPreviousData` evita piscar a tela ao trocar de página/filtro. */
export function usePublicacoes(filters: PublicationsFilters) {
  return useQuery({ ...publicationsQuery(filters), placeholderData: keepPreviousData })
}

export function usePublicacaoSummary() {
  return useQuery(publicationsSummaryQuery())
}

export function usePublicacao(id: string) {
  return useQuery(publicationQuery(id))
}

/**
 * Invalida tudo que depende de publicações: listas, resumo, detalhe, e o badge da navegação
 * (que conta as pendentes). Por prefixo, para não esquecer nenhuma combinação de filtros.
 */
function useInvalidatePublications() {
  const queryClient = useQueryClient()

  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: queryKeys.publications.all }),
      queryClient.invalidateQueries({ queryKey: queryKeys.navCounts }),
      queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
    ])
}

export function useMarkPublicacaoAsRead() {
  const invalidate = useInvalidatePublications()

  return useMutation({
    mutationFn: (id: string) => markPublicationAsRead(id),
    onSuccess: invalidate,
    // "Já estava lida" é 'silent' no catálogo de erros: dois cliques não merecem toast.
    onError: (error) => {
      handleApiError(error)
      void invalidate()
    },
  })
}

function useChangeStatus(status: PublicationApiStatus, successMessage: string) {
  const invalidate = useInvalidatePublications()

  return useMutation({
    mutationFn: (id: string) => changePublicationStatus(id, status),
    onSuccess: () => {
      notify.success(successMessage)
      return invalidate()
    },
    onError: (error) => {
      handleApiError(error)
      // Uma transição recusada (409) quase sempre é tela desatualizada: recarregar resolve.
      void invalidate()
    },
  })
}

/** "Tratada" = aprovada pelo advogado (revisão humana — ADR-012). */
export function useMarkPublicacaoAsTreated() {
  return useChangeStatus('Approved', 'Publicação marcada como tratada.')
}

export function useArchivePublicacao() {
  return useChangeStatus('Archived', 'Publicação arquivada.')
}

export type BulkAction = 'read' | 'treated' | 'archived'

export interface BulkResult {
  succeeded: number
  failed: number
}

const bulkSuccessLabel: Record<BulkAction, string> = {
  read: 'marcada(s) como lida(s)',
  treated: 'marcada(s) como tratada(s)',
  archived: 'arquivada(s)',
}

/**
 * Ação em massa: aplica a mesma operação a cada publicação selecionada. A API não tem
 * endpoint de lote de propósito — cada publicação passa pela própria regra de transição e
 * pelo próprio registro de auditoria — então o lote é uma sequência de chamadas individuais.
 *
 * allSettled: uma publicação recusada (já revisada em outra aba, por exemplo) não impede as
 * demais. "Já lida" conta como sucesso: o resultado desejado já existia.
 */
export function useBulkPublicacaoAction() {
  const invalidate = useInvalidatePublications()

  return useMutation({
    mutationFn: async ({
      action,
      ids,
    }: {
      action: BulkAction
      ids: string[]
    }): Promise<BulkResult> => {
      const run = (id: string) =>
        action === 'read'
          ? markPublicationAsRead(id)
          : changePublicationStatus(id, action === 'treated' ? 'Approved' : 'Archived')

      const results = await Promise.allSettled(ids.map(run))

      const failed = results.filter(
        (result) =>
          result.status === 'rejected' &&
          !(result.reason instanceof ApiError && result.reason.code === 'PUBLICATION_ALREADY_READ')
      ).length

      return { succeeded: ids.length - failed, failed }
    },
    onSuccess: ({ succeeded, failed }, { action }) => {
      if (succeeded > 0) notify.success(`${succeeded} publicação(ões) ${bulkSuccessLabel[action]}.`)
      if (failed > 0) {
        notify.warning(`${failed} publicação(ões) não puderam ser alteradas.`, {
          description: 'Elas podem já ter sido revisadas. A lista foi atualizada.',
        })
      }
      return invalidate()
    },
    onError: (error) => handleApiError(error),
  })
}
