import { toCsv } from '@/lib/csv'
import { formatDate } from '@/lib/format'
import { tipoLancamentoLabels, type Lancamento } from './types'

/**
 * Planilha dos lançamentos que a tela mostra (já filtrados). O valor sai como número com
 * vírgula decimal, que é o que o Excel pt-BR espera; texto "R$ 1.234,56" viraria string.
 */
export function lancamentosToCsv(lancamentos: Lancamento[]): string {
  return toCsv(
    ['Tipo', 'Descrição', 'Cliente / Fornecedor', 'Processo', 'Valor', 'Vencimento', 'Status'],
    lancamentos.map((l) => [
      tipoLancamentoLabels[l.tipo],
      l.descricao,
      l.cliente ?? l.fornecedor ?? '',
      l.processo ?? '',
      l.valor.toFixed(2).replace('.', ','),
      formatDate(l.vencimento),
      l.status,
    ])
  )
}
