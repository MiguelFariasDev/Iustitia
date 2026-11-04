'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { createLancamento } from './api'
import { financeiroResumoQuery, fluxoCaixaQuery, lancamentosQuery } from './queries'
import type { LancamentoFormValues } from './schemas'

// TODO(financeiro-backend): estes hooks continuam; só api.ts deixa de usar o mock (Fase 4/8).
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
