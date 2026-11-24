'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { createLancamento, marcarComoPago } from './api'
import { financeiroResumoQuery, fluxoCaixaQuery, lancamentosQuery } from './queries'
import type { LancamentoFormValues } from './schemas'

export function useFinanceiroResumo() {
  return useQuery(financeiroResumoQuery())
}

export function useLancamentos() {
  return useQuery(lancamentosQuery())
}

export function useFluxoCaixa() {
  return useQuery(fluxoCaixaQuery())
}

/** Invalida o prefixo `financeiro`: um lançamento novo muda a lista, o resumo e o fluxo. */
export function useCreateLancamento() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (values: LancamentoFormValues) => createLancamento(values),
    onSuccess: (lancamento) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.financeiro.all })
      notify.success('Lançamento registrado.', { description: lancamento.descricao })
    },
    onError: (error) => handleApiError(error),
  })
}

/** Marca como pago: muda a lista, o faturamento do mês, o saldo e o fluxo de caixa. */
export function useMarcarComoPago() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => marcarComoPago(id),
    onSuccess: (lancamento) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.financeiro.all })
      notify.success('Lançamento marcado como pago.', { description: lancamento.descricao })
    },
    onError: (error) => handleApiError(error),
  })
}
