export type TipoLancamento = 'Honorario' | 'ContaPagar' | 'ContaReceber'
export type StatusLancamento = 'Pendente' | 'Pago' | 'Atrasado'

export interface Lancamento {
  id: string
  tipo: TipoLancamento
  descricao: string
  cliente: string | null
  processo: string | null
  valor: number
  vencimento: string
  status: StatusLancamento
}

export interface FluxoCaixaMes {
  mes: string
  entradas: number
  saidas: number
}

export interface FinanceiroResumo {
  honorariosAReceber: number
  honorariosRecebidosMes: number
  contasAPagar: number
  contasAReceber: number
  saldo: number
}
