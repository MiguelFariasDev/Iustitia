import { useAuthStore } from '@/stores/authStore'
import type { LancamentoFormValues } from './schemas'
import type {
  FinanceiroResumo,
  FluxoCaixaMes,
  Lancamento,
  PeriodoFinanceiro,
  StatusLancamento,
} from './types'

/**
 * Repositório dos lançamentos financeiros. NÃO há seed: a tela começa vazia e só mostra o que o
 * próprio usuário lançou. O módulo Financeiro ainda não tem servidor, então os lançamentos são
 * guardados NESTE NAVEGADOR, separados por usuário — não sincronizam entre aparelhos nem entre
 * pessoas do escritório. A tela diz isso (ver FinanceiroView).
 *
 * TODO(financeiro-backend): trocar por `endpoints.financeiro` em api.ts; hooks e telas não mudam.
 */
const PREFIXO = 'iustitia:financeiro:v1:'

function chave(): string {
  return `${PREFIXO}${useAuthStore.getState().user?.id ?? 'anonimo'}`
}

function ler(): Lancamento[] {
  try {
    const bruto = window.localStorage.getItem(chave())
    const dados: unknown = bruto ? JSON.parse(bruto) : []

    return Array.isArray(dados) ? (dados as Lancamento[]) : []
  } catch {
    return []
  }
}

function gravar(lancamentos: Lancamento[]) {
  try {
    window.localStorage.setItem(chave(), JSON.stringify(lancamentos))
  } catch {
    // Armazenamento cheio ou bloqueado (janela privada): o lançamento vale só nesta sessão da tela.
  }
}

/** Pendente com vencimento já passado é Atrasado — derivado ao ler, nunca fica velho no disco. */
function comStatusAtual(lancamento: Lancamento, agora: Date): Lancamento {
  if (lancamento.status !== 'Pendente') return lancamento
  const inicioDoDia = new Date(agora.getFullYear(), agora.getMonth(), agora.getDate())

  return new Date(lancamento.vencimento) < inicioDoDia
    ? { ...lancamento, status: 'Atrasado' }
    : lancamento
}

export function listarLancamentos(agora: Date = new Date()): Lancamento[] {
  return ler()
    .map((l) => comStatusAtual(l, agora))
    .sort((a, b) => a.vencimento.localeCompare(b.vencimento))
}

export function criarLancamento(values: LancamentoFormValues): Lancamento {
  const pago = values.status === 'Pago'
  const pessoa = values.pessoa.trim() || null
  const criado: Lancamento = {
    id: crypto.randomUUID(),
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
  gravar([...ler(), criado])

  return criado
}

/** Entradas e saídas PAGAS dos últimos 6 meses, calculadas dos lançamentos — sem série inventada. */
export function fluxoDeCaixa(lancamentos: Lancamento[], agora: Date = new Date()): FluxoCaixaMes[] {
  const rotulo = new Intl.DateTimeFormat('pt-BR', { month: 'short' })

  return Array.from({ length: 6 }, (_, i) => {
    const inicio = new Date(agora.getFullYear(), agora.getMonth() - (5 - i), 1)
    const noMes = (iso: string | null) => {
      if (!iso) return false
      const d = new Date(iso)

      return d.getFullYear() === inicio.getFullYear() && d.getMonth() === inicio.getMonth()
    }
    const pagos = lancamentos.filter((l) => l.status === 'Pago' && noMes(l.pagoEm))
    const nome = rotulo.format(inicio).replace('.', '')

    return {
      mes: nome.charAt(0).toUpperCase() + nome.slice(1),
      entradas: pagos.filter((l) => l.tipo !== 'ContaPagar').reduce((t, l) => t + l.valor, 0),
      saidas: pagos.filter((l) => l.tipo === 'ContaPagar').reduce((t, l) => t + l.valor, 0),
    }
  })
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
