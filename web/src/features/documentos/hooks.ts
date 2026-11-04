'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { deleteDocumento, uploadDocumento } from './api'
import { documentosQuery } from './queries'
import type { UploadFormValues } from './schemas'
import type { DocumentosFilters } from './types'

export function useDocumentos(filters: DocumentosFilters = {}) {
  return useQuery({ ...documentosQuery(filters), placeholderData: (previous) => previous })
}

/** Invalida por PREFIXO: a lista tem uma entrada de cache por combinação de filtros. */
export function useUploadDocumento() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (values: UploadFormValues) => uploadDocumento(values),
    onSuccess: (documento) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.documentos.all })
      notify.success('Documento enviado.', { description: documento.nome })
    },
    onError: (error) => handleApiError(error),
  })
}

export function useDeleteDocumento() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => deleteDocumento(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.documentos.all })
      notify.success('Documento excluído.')
    },
    onError: (error) => handleApiError(error),
  })
}
