export type TipoLancamento = 'Honorario' | 'ContaPagar' | 'ContaReceber'
export type StatusLancamento = 'Pendente' | 'Pago' | 'Atrasado'

export const tiposLancamento: readonly TipoLancamento[] = [
  'Honorario',
  'ContaPagar',
  'ContaReceber',
]
export const statusLancamento: readonly StatusLancamento[] = ['Pendente', 'Pago', 'Atrasado']

export const tipoLancamentoLabels: Record<TipoLancamento, string> = {
  Honorario: 'Honorário',
  ContaPagar: 'Conta a pagar',
  ContaReceber: 'Conta a receber',
}

/** Avulso, parcela de um valor total ou mês de uma cobrança mensal. */
export type RecorrenciaLancamento = 'Unica' | 'Parcelado' | 'Mensal'

export const recorrenciasLancamento: readonly RecorrenciaLancamento[] = ['Unica', 'Parcelado', 'Mensal']

export const recorrenciaLabels: Record<RecorrenciaLancamento, string> = {
  Unica: 'À vista (uma vez)',
  Parcelado: 'Parcelado',
  Mensal: 'Cobrança mensal',
}

/** Teto do servidor (Lancamento.MaxParcelas). */
export const MAX_PARCELAS = 60

export interface Lancamento {
  id: string
  tipo: TipoLancamento
  descricao: string
  /** Honorário e conta a receber. */
  cliente: string | null
  /** Conta a pagar. */
  fornecedor: string | null
  processo: string | null
  valor: number
  vencimento: string
  status: StatusLancamento
  /** Quando foi pago; `null` enquanto em aberto. Base do "faturamento do mês". */
  pagoEm: string | null
  recorrencia: RecorrenciaLancamento
  /** Posição na série (1 = primeira); `null` no avulso. */
  parcela: number | null
  totalParcelas: number | null
  serieId: string | null
}

export interface FluxoCaixaMes {
  mes: string
  entradas: number
  saidas: number
}

export interface FinanceiroResumo {
  /** Honorários pagos no mês corrente. */
  faturamentoMes: number
  honorariosAReceber: number
  contasAPagar: number
  contasAReceber: number
  saldo: number
}

/** Janela de vencimento do filtro de período. */
export type PeriodoFinanceiro = 'todos' | 'mes' | 'proximos30'

export const periodoFinanceiroLabels: Record<PeriodoFinanceiro, string> = {
  todos: 'Qualquer data',
  mes: 'Vence neste mês',
  proximos30: 'Vence nos próximos 30 dias',
}
