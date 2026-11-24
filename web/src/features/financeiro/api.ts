import { endpoints } from '@/lib/api/endpoints'
import { apiClient } from '@/lib/api/client'
import {
  toFluxoCaixa,
  toLancamento,
  toResumo,
  type FluxoCaixaDto,
  type LancamentoDto,
  type ListLancamentosDto,
  type ResumoDto,
} from './contract'
import type { LancamentoFormValues } from './schemas'
import type { FinanceiroResumo, FluxoCaixaMes, Lancamento } from './types'

const PAGE_SIZE = 200
/** Trava de segurança: 10 páginas = 2.000 lançamentos na tela; acima disso falta paginação na interface. */
const MAX_PAGES = 10

/**
 * Lançamentos do escritório, por vencimento. A tela filtra e ordena no cliente, então traz todas as
 * páginas (até um teto). O RESUMO e o FLUXO não dependem disso: vêm agregados do servidor.
 */
export async function getLancamentos(): Promise<Lancamento[]> {
  const itens: Lancamento[] = []

  for (let page = 1; page <= MAX_PAGES; page += 1) {
    const { data } = await apiClient.get<ListLancamentosDto>(endpoints.financeiro.lancamentos, {
      params: { page, pageSize: PAGE_SIZE },
    })
    itens.push(...data.items.map(toLancamento))
    if (page >= data.totalPages) break
  }

  return itens
}

export async function getFinanceiroResumo(): Promise<FinanceiroResumo> {
  const { data } = await apiClient.get<ResumoDto>(endpoints.financeiro.resumo)

  return toResumo(data)
}

export async function getFluxoCaixa(): Promise<FluxoCaixaMes[]> {
  const { data } = await apiClient.get<FluxoCaixaDto[]>(endpoints.financeiro.fluxoCaixa, {
    params: { meses: 6 },
  })

  return toFluxoCaixa(data)
}

export async function createLancamento(values: LancamentoFormValues): Promise<Lancamento> {
  const { data } = await apiClient.post<LancamentoDto>(endpoints.financeiro.lancamentos, {
    tipo: values.tipo,
    descricao: values.descricao.trim(),
    // A caixa "Cliente/Fornecedor" do formulário é a "pessoa" do servidor.
    pessoa: values.pessoa.trim() || null,
    processo: values.processo.trim() || null,
    valor: values.valor,
    vencimento: values.vencimento,
    jaPago: values.status === 'Pago',
  })

  return toLancamento(data)
}

export async function marcarComoPago(id: string): Promise<Lancamento> {
  const { data } = await apiClient.post<LancamentoDto>(endpoints.financeiro.pagar(id))

  return toLancamento(data)
}
