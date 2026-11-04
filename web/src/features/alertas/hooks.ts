'use client'

import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { dismissAlerta, markAlertaAsRead, markAllAlertasAsRead } from './api'
import { alertaQuery, alertasQuery, alertasSummaryQuery } from './queries'
import type { AlertasFilters } from './types'

export function useAlertas(filters: AlertasFilters, options: { enabled?: boolean } = {}) {
  return useQuery({
    ...alertasQuery(filters),
    placeholderData: keepPreviousData,
    enabled: options.enabled ?? true,
  })
}

export function useAlerta(id: string) {
  return useQuery(alertaQuery(id))
}

export function useAlertaSummary() {
  return useQuery(alertasSummaryQuery())
}

/** Contador do sino: não lidos. `undefined` enquanto carrega — o badge simplesmente não aparece. */
export function useAlertasNaoLidos(): number | undefined {
  return useAlertaSummary().data?.unread
}

/** Alertas, resumo e detalhe dependem uns dos outros: invalida o prefixo todo. */
function useInvalidateAlertas() {
  const queryClient = useQueryClient()

  return () => queryClient.invalidateQueries({ queryKey: queryKeys.alerts.all })
}

export function useMarkAlertaAsRead() {
  const invalidate = useInvalidateAlertas()

  return useMutation({
    mutationFn: (id: string) => markAlertaAsRead(id),
    onSuccess: invalidate,
    // "Já lido" é 'silent' no catálogo: dois cliques ou duas abas não merecem toast.
    onError: (error) => {
      handleApiError(error)
      void invalidate()
    },
  })
}

export function useMarkAllAlertasAsRead() {
  const invalidate = useInvalidateAlertas()

  return useMutation({
    mutationFn: () => markAllAlertasAsRead(),
    onSuccess: (changed) => {
      if (changed > 0) notify.success(`${changed} alerta(s) marcado(s) como lido(s).`)
      return invalidate()
    },
    onError: (error) => handleApiError(error),
  })
}

export function useDismissAlerta() {
  const invalidate = useInvalidateAlertas()

  return useMutation({
    mutationFn: (id: string) => dismissAlerta(id),
    onSuccess: invalidate,
    onError: (error) => {
      handleApiError(error)
      void invalidate()
    },
  })
}
