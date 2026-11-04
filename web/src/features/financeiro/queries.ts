import { queryOptions } from '@tanstack/react-query'
import { queryKeys } from '@/lib/query/queryKeys'
import { getFinanceiroResumo, getFluxoCaixa, getLancamentos } from './api'

export const financeiroResumoQuery = () =>
  queryOptions({ queryKey: queryKeys.financeiro.resumo(), queryFn: getFinanceiroResumo })

export const lancamentosQuery = () =>
  queryOptions({ queryKey: queryKeys.financeiro.lancamentos(), queryFn: getLancamentos })

export const fluxoCaixaQuery = () =>
  queryOptions({ queryKey: queryKeys.financeiro.fluxoCaixa(), queryFn: getFluxoCaixa })
