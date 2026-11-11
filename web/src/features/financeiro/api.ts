import { calcularResumo, criarLancamento, fluxoDeCaixa, listarLancamentos } from './repositorio'
import type { LancamentoFormValues } from './schemas'
import type { FinanceiroResumo, FluxoCaixaMes, Lancamento } from './types'

/**
 * O módulo Financeiro ainda não tem endpoint: os lançamentos vêm do repositório local
 * (repositorio.ts), que começa vazio e guarda só o que o usuário lança neste navegador.
 *
 * TODO(financeiro-backend): trocar cada função por uma chamada ao `apiClient`. O resumo hoje é
 * derivado dos lançamentos; no servidor ele precisa ser agregado lá (a lista será paginada).
 */
export async function getLancamentos(): Promise<Lancamento[]> {
  return listarLancamentos()
}

export async function getFinanceiroResumo(): Promise<FinanceiroResumo> {
  return calcularResumo(listarLancamentos())
}

export async function getFluxoCaixa(): Promise<FluxoCaixaMes[]> {
  return fluxoDeCaixa(listarLancamentos())
}

export async function createLancamento(values: LancamentoFormValues): Promise<Lancamento> {
  return criarLancamento(values)
}
