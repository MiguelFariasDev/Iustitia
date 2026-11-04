import type { FinanceiroResumo, FluxoCaixaMes, Lancamento } from './types'

/**
 * DADOS DE DEMONSTRAÇÃO. O módulo Financeiro não existe no backend (Fase 4): nada aqui vem
 * do banco do escritório, e a tela avisa isso. Ao existir o endpoint, esta é a única
 * fonte a trocar (ver api.ts).
 */
export const lancamentosMock: Lancamento[] = [
  {
    id: '1',
    tipo: 'Honorario',
    descricao: 'Honorários iniciais — ação de cobrança',
    cliente: 'Maria Souza',
    processo: '0000832-35.2018.4.01.3202',
    valor: 3500,
    vencimento: '2026-10-05',
    status: 'Pendente',
  },
  {
    id: '2',
    tipo: 'Honorario',
    descricao: 'Êxito — reclamação trabalhista',
    cliente: 'José Lima',
    processo: null,
    valor: 8200,
    vencimento: '2026-09-15',
    status: 'Atrasado',
  },
  {
    id: '3',
    tipo: 'Honorario',
    descricao: 'Mensalidade consultoria',
    cliente: 'Padaria Boa Massa LTDA',
    processo: null,
    valor: 1500,
    vencimento: '2026-09-10',
    status: 'Pago',
  },
  {
    id: '4',
    tipo: 'ContaPagar',
    descricao: 'Aluguel do escritório',
    cliente: null,
    processo: null,
    valor: 2800,
    vencimento: '2026-10-08',
    status: 'Pendente',
  },
  {
    id: '5',
    tipo: 'ContaPagar',
    descricao: 'Custas judiciais',
    cliente: 'Maria Souza',
    processo: '0000832-35.2018.4.01.3202',
    valor: 420,
    vencimento: '2026-09-28',
    status: 'Atrasado',
  },
  {
    id: '6',
    tipo: 'ContaReceber',
    descricao: 'Reembolso de despesas',
    cliente: 'José Lima',
    processo: null,
    valor: 640,
    vencimento: '2026-10-12',
    status: 'Pendente',
  },
  {
    id: '7',
    tipo: 'ContaReceber',
    descricao: 'Parcela 2/3 — acordo',
    cliente: 'Padaria Boa Massa LTDA',
    processo: null,
    valor: 2000,
    vencimento: '2026-09-20',
    status: 'Pago',
  },
]

export const fluxoCaixaMock: FluxoCaixaMes[] = [
  { mes: 'Abr', entradas: 9800, saidas: 5200 },
  { mes: 'Mai', entradas: 11200, saidas: 5900 },
  { mes: 'Jun', entradas: 8700, saidas: 6100 },
  { mes: 'Jul', entradas: 12400, saidas: 5600 },
  { mes: 'Ago', entradas: 10100, saidas: 6300 },
  { mes: 'Set', entradas: 13500, saidas: 6000 },
]

export function calcularResumo(lancamentos: Lancamento[]): FinanceiroResumo {
  const soma = (fn: (l: Lancamento) => boolean) =>
    lancamentos.filter(fn).reduce((total, l) => total + l.valor, 0)
  const abertos = (l: Lancamento) => l.status !== 'Pago'

  const recebido = soma((l) => l.status === 'Pago' && l.tipo !== 'ContaPagar')
  const pago = soma((l) => l.status === 'Pago' && l.tipo === 'ContaPagar')

  return {
    honorariosAReceber: soma((l) => l.tipo === 'Honorario' && abertos(l)),
    honorariosRecebidosMes: soma((l) => l.tipo === 'Honorario' && l.status === 'Pago'),
    contasAPagar: soma((l) => l.tipo === 'ContaPagar' && abertos(l)),
    contasAReceber: soma((l) => l.tipo === 'ContaReceber' && abertos(l)),
    saldo: recebido - pago,
  }
}
