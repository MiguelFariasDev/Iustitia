import type {
  FinanceiroResumo,
  FluxoCaixaMes,
  Lancamento,
  StatusLancamento,
  TipoLancamento,
} from './types'
import { tiposLancamento } from './types'

/** LancamentoResponse (Financeiro.Application.Features.Lancamentos). */
export interface LancamentoDto {
  id: string
  tipo: string
  descricao: string
  pessoa: string | null
  processo: string | null
  valor: number
  /** Data pura (AAAA-MM-DD), sem fuso. */
  vencimento: string
  status: string
  pagoEm: string | null
  createdAt: string
}

export interface ListLancamentosDto {
  items: LancamentoDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** ResumoFinanceiroResponse. */
export interface ResumoDto {
  faturamentoMes: number
  honorariosAReceber: number
  contasAPagar: number
  contasAReceber: number
  saldo: number
}

/** FluxoCaixaMesItem. */
export interface FluxoCaixaDto {
  ano: number
  mes: number
  entradas: number
  saidas: number
}

const statusConhecidos: readonly StatusLancamento[] = ['Pendente', 'Pago', 'Atrasado']

function toTipo(tipo: string): TipoLancamento {
  return (tiposLancamento as readonly string[]).includes(tipo)
    ? (tipo as TipoLancamento)
    : 'Honorario'
}

/** Status desconhecido cai em 'Pendente': o seguro é tratar como em aberto, nunca como quitado. */
function toStatus(status: string): StatusLancamento {
  return (statusConhecidos as readonly string[]).includes(status)
    ? (status as StatusLancamento)
    : 'Pendente'
}

export function toLancamento(dto: LancamentoDto): Lancamento {
  const tipo = toTipo(dto.tipo)

  return {
    id: dto.id,
    tipo,
    descricao: dto.descricao,
    // A mesma "pessoa" do servidor vira o campo certo para cada tipo (como na caixa do formulário).
    cliente: tipo === 'ContaPagar' ? null : dto.pessoa,
    fornecedor: tipo === 'ContaPagar' ? dto.pessoa : null,
    processo: dto.processo,
    valor: dto.valor,
    // Meio-dia local: "AAAA-MM-DD" lido como UTC voltaria um dia em São Paulo.
    vencimento: `${dto.vencimento}T12:00:00`,
    status: toStatus(dto.status),
    pagoEm: dto.pagoEm,
  }
}

export function toResumo(dto: ResumoDto): FinanceiroResumo {
  return { ...dto }
}

const nomeDoMes = new Intl.DateTimeFormat('pt-BR', { month: 'short' })

export function toFluxoCaixa(dtos: FluxoCaixaDto[]): FluxoCaixaMes[] {
  return dtos.map((dto) => {
    const nome = nomeDoMes.format(new Date(dto.ano, dto.mes - 1, 1)).replace('.', '')

    return {
      mes: nome.charAt(0).toUpperCase() + nome.slice(1),
      entradas: dto.entradas,
      saidas: dto.saidas,
    }
  })
}
