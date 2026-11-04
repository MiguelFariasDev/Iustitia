import { mockResponse } from '@/lib/api/mock'
import { calcularResumo, mockCreateLancamento, mockFluxoCaixa, mockListLancamentos } from './mocks'
import type { LancamentoFormValues } from './schemas'
import type { FinanceiroResumo, FluxoCaixaMes, Lancamento } from './types'

/**
 * Financeiro ainda não tem endpoint: tudo vem do repositório em memória (mocks.ts), sempre —
 * ligar a API real hoje daria 404.
 *
 * TODO(financeiro-backend, Fase 4/8): trocar cada função por uma chamada ao `apiClient` e
 * remover o aviso de demonstração da tela. O resumo hoje é derivado dos lançamentos; no
 * servidor ele precisa ser agregado lá (a lista será paginada).
 */
export function getLancamentos(): Promise<Lancamento[]> {
  return mockResponse(mockListLancamentos(), 250)
}

export function getFinanceiroResumo(): Promise<FinanceiroResumo> {
  return mockResponse(calcularResumo(mockListLancamentos()), 150)
}

export function getFluxoCaixa(): Promise<FluxoCaixaMes[]> {
  return mockResponse(mockFluxoCaixa(), 150)
}

export function createLancamento(values: LancamentoFormValues): Promise<Lancamento> {
  return mockResponse(mockCreateLancamento(values), 250)
}
