import { daysFromNow, ultimosMeses } from '@/mocks/dates'
import type { LancamentoFormValues } from './schemas'
import type {
  FinanceiroResumo,
  FluxoCaixaMes,
  Lancamento,
  PeriodoFinanceiro,
  StatusLancamento,
} from './types'

/**
 * DADOS DE DEMONSTRAÇÃO. O módulo Financeiro não existe no backend (Fase 4/8): nada aqui
 * vem do banco do escritório, e a tela avisa isso. É um repositório EM MEMÓRIA — recarregar a
 * página volta ao estado inicial. As datas são relativas a "hoje" para o mock nunca ficar
 * velho (datas fixas já faziam "do mês" mentir).
 *
 * TODO(financeiro-backend): trocar por `endpoints.financeiro` em api.ts (Fase 4/8).
 */
type Seed = Omit<Lancamento, 'vencimento' | 'pagoEm'> & { vence: number; pago?: boolean }

function seedLancamentos(): Lancamento[] {
  const base: Seed[] = [
    {
      id: '1',
      tipo: 'Honorario',
      descricao: 'Honorários iniciais — ação de cobrança',
      cliente: 'Maria Souza',
      fornecedor: null,
      processo: '0000832-35.2018.4.01.3202',
      valor: 3500,
      vence: 6,
      status: 'Pendente',
    },
    {
      id: '2',
      tipo: 'Honorario',
      descricao: 'Êxito — reclamação trabalhista',
      cliente: 'José Lima',
      fornecedor: null,
      processo: null,
      valor: 8200,
      vence: -20,
      status: 'Atrasado',
    },
    {
      id: '3',
      tipo: 'Honorario',
      descricao: 'Mensalidade consultoria',
      cliente: 'Padaria Boa Massa LTDA',
      fornecedor: null,
      processo: null,
      valor: 1500,
      vence: -2,
      status: 'Pago',
      pago: true,
    },
    {
      id: '4',
      tipo: 'ContaPagar',
      descricao: 'Aluguel do escritório',
      cliente: null,
      fornecedor: 'Imobiliária Central',
      processo: null,
      valor: 2800,
      vence: 4,
      status: 'Pendente',
    },
    {
      id: '5',
      tipo: 'ContaPagar',
      descricao: 'Custas judiciais',
      cliente: null,
      fornecedor: 'Tribunal de Justiça',
      processo: '0000832-35.2018.4.01.3202',
      valor: 420,
      vence: -7,
      status: 'Atrasado',
    },
    {
      id: '6',
      tipo: 'ContaReceber',
      descricao: 'Reembolso de despesas',
      cliente: 'José Lima',
      fornecedor: null,
      processo: null,
      valor: 640,
      vence: 8,
      status: 'Pendente',
    },
    {
      id: '7',
      tipo: 'ContaReceber',
      descricao: 'Parcela 2/3 — acordo',
      cliente: 'Padaria Boa Massa LTDA',
      fornecedor: null,
      processo: null,
      valor: 2000,
      vence: -12,
      status: 'Pago',
      pago: true,
    },
  ]

  return base.map(({ vence, pago, ...resto }) => ({
    ...resto,
    vencimento: daysFromNow(vence, 12),
    pagoEm: pago ? daysFromNow(0, 12) : null,
  }))
}

let store: Lancamento[] = seedLancamentos()
let sequence = 100

export function resetFinanceiroMock() {
  store = seedLancamentos()
  sequence = 100
}

export function mockListLancamentos(): Lancamento[] {
  return [...store].sort((a, b) => a.vencimento.localeCompare(b.vencimento))
}

export function mockCreateLancamento(values: LancamentoFormValues): Lancamento {
  sequence += 1
  const pago = values.status === 'Pago'
  const pessoa = values.pessoa.trim() || null
  const created: Lancamento = {
    id: String(sequence),
    tipo: values.tipo,
    descricao: values.descricao.trim(),
    // A mesma caixa "Cliente/Fornecedor" do formulário vira o campo certo para cada tipo.
    cliente: values.tipo === 'ContaPagar' ? null : pessoa,
    fornecedor: values.tipo === 'ContaPagar' ? pessoa : null,
    processo: values.processo.trim() || null,
    valor: values.valor ?? 0,
    // Meio-dia local: uma data "AAAA-MM-DD" lida como UTC voltaria um dia em São Paulo.
    vencimento: new Date(`${values.vencimento}T12:00:00`).toISOString(),
    status: values.status,
    pagoEm: pago ? new Date().toISOString() : null,
  }
  store = [...store, created]

  return created
}

/** Série fixa de 6 meses; só os rótulos acompanham o calendário. */
export function mockFluxoCaixa(agora: Date = new Date()): FluxoCaixaMes[] {
  const valores = [
    [9800, 5200],
    [11200, 5900],
    [8700, 6100],
    [12400, 5600],
    [10100, 6300],
    [13500, 6000],
  ] as const

  return ultimosMeses(6, agora).map((mes, i) => ({
    mes: mes.charAt(0).toUpperCase() + mes.slice(1),
    entradas: valores[i]![0],
    saidas: valores[i]![1],
  }))
}

export function calcularResumo(
  lancamentos: Lancamento[],
  agora: Date = new Date()
): FinanceiroResumo {
  const soma = (fn: (l: Lancamento) => boolean) =>
    lancamentos.filter(fn).reduce((total, l) => total + l.valor, 0)
  const abertos = (l: Lancamento) => l.status !== 'Pago'
  const noMesCorrente = (iso: string | null) => {
    if (!iso) return false
    const d = new Date(iso)
    return d.getFullYear() === agora.getFullYear() && d.getMonth() === agora.getMonth()
  }

  const recebido = soma((l) => l.status === 'Pago' && l.tipo !== 'ContaPagar')
  const pago = soma((l) => l.status === 'Pago' && l.tipo === 'ContaPagar')

  return {
    faturamentoMes: soma((l) => l.tipo === 'Honorario' && noMesCorrente(l.pagoEm)),
    honorariosAReceber: soma((l) => l.tipo === 'Honorario' && abertos(l)),
    contasAPagar: soma((l) => l.tipo === 'ContaPagar' && abertos(l)),
    contasAReceber: soma((l) => l.tipo === 'ContaReceber' && abertos(l)),
    saldo: recebido - pago,
  }
}

/** Filtros da tela, aplicados no cliente sobre a lista já carregada. */
export function filtrarLancamentos(
  lancamentos: Lancamento[],
  filtros: { status: StatusLancamento | null; periodo: PeriodoFinanceiro },
  agora: Date = new Date()
): Lancamento[] {
  const fimDoDia = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59)
  const limite30 = fimDoDia(new Date(agora.getTime() + 30 * 86_400_000))

  return lancamentos.filter((l) => {
    if (filtros.status && l.status !== filtros.status) return false

    const venc = new Date(l.vencimento)
    if (filtros.periodo === 'mes') {
      return venc.getFullYear() === agora.getFullYear() && venc.getMonth() === agora.getMonth()
    }
    if (filtros.periodo === 'proximos30') {
      return (
        venc >= new Date(agora.getFullYear(), agora.getMonth(), agora.getDate()) && venc <= limite30
      )
    }

    return true
  })
}
