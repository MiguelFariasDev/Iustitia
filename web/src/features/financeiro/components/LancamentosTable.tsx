'use client'

import type { ColumnDef } from '@tanstack/react-table'
import { Check } from 'lucide-react'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Button } from '@/components/ui/button'
import { formatCurrency, formatDate } from '@/lib/format'
import { useMarcarComoPago } from '../hooks'
import {
  tipoLancamentoLabels,
  type Lancamento,
  type StatusLancamento,
  type TipoLancamento,
} from '../types'

const tomStatus: Record<StatusLancamento, Tone> = {
  Pendente: 'warning',
  Pago: 'success',
  Atrasado: 'danger',
}

type Coluna = ColumnDef<Lancamento, unknown>

const traco = (valor: string | null) => valor ?? '—'

const valor: Coluna = {
  accessorKey: 'valor',
  header: 'Valor',
  cell: ({ row }) => <span className="tabular-nums">{formatCurrency(row.original.valor)}</span>,
}

const vencimento: Coluna = {
  accessorKey: 'vencimento',
  header: 'Vencimento',
  cell: ({ row }) => formatDate(row.original.vencimento),
}

const status: Coluna = {
  accessorKey: 'status',
  header: 'Status',
  cell: ({ row }) => (
    <StatusBadge tone={tomStatus[row.original.status]}>{row.original.status}</StatusBadge>
  ),
}

const descricao: Coluna = { accessorKey: 'descricao', header: 'Descrição' }

function PagarButton({ lancamento }: { lancamento: Lancamento }) {
  const { mutate, isPending } = useMarcarComoPago()
  if (lancamento.status === 'Pago') return null

  return (
    <Button
      variant="ghost"
      size="sm"
      disabled={isPending}
      onClick={() => mutate(lancamento.id)}
      aria-label={`Marcar como pago: ${lancamento.descricao}`}
    >
      <Check aria-hidden />
      Marcar como pago
    </Button>
  )
}

const acoes: Coluna = {
  id: 'acoes',
  header: 'Ações',
  enableSorting: false,
  cell: ({ row }) => <PagarButton lancamento={row.original} />,
}

/**
 * Colunas por aba. Em nível de módulo (identidade estável): o DataTable recria a tabela
 * inteira se `columns` mudar de referência a cada render, e perderia ordenação e foco.
 */
const colunasPorTipo: Record<TipoLancamento | 'Atrasado', Coluna[]> = {
  Atrasado: [
    {
      accessorKey: 'tipo',
      header: 'Tipo',
      cell: ({ row }) => tipoLancamentoLabels[row.original.tipo],
    },
    descricao,
    {
      id: 'parte',
      header: 'Cliente / Fornecedor',
      cell: ({ row }) => traco(row.original.cliente ?? row.original.fornecedor),
    },
    valor,
    vencimento,
    status,
    acoes,
  ],
  Honorario: [
    { accessorKey: 'cliente', header: 'Cliente', cell: ({ row }) => traco(row.original.cliente) },
    {
      accessorKey: 'processo',
      header: 'Processo',
      cell: ({ row }) => <span className="font-mono text-xs">{traco(row.original.processo)}</span>,
    },
    valor,
    vencimento,
    status,
    acoes,
  ],
  ContaPagar: [
    descricao,
    {
      accessorKey: 'fornecedor',
      header: 'Fornecedor',
      cell: ({ row }) => traco(row.original.fornecedor),
    },
    valor,
    vencimento,
    status,
    acoes,
  ],
  ContaReceber: [
    descricao,
    { accessorKey: 'cliente', header: 'Cliente', cell: ({ row }) => traco(row.original.cliente) },
    valor,
    vencimento,
    status,
    acoes,
  ],
}

interface LancamentosTableProps {
  /** `'Atrasado'`: todos os tipos (a aba "Em atraso"). */
  tipo: TipoLancamento | 'Atrasado'
  lancamentos: Lancamento[]
  isLoading?: boolean
}

export function LancamentosTable({ tipo, lancamentos, isLoading }: LancamentosTableProps) {
  const rotulo = tipo === 'Atrasado' ? 'Lançamentos em atraso' : `${tipoLancamentoLabels[tipo]}s`

  return (
    <DataTable
      caption={rotulo}
      columns={colunasPorTipo[tipo]}
      data={tipo === 'Atrasado' ? lancamentos : lancamentos.filter((l) => l.tipo === tipo)}
      isLoading={isLoading}
      getRowId={(l) => l.id}
      searchPlaceholder="Buscar por descrição, cliente ou fornecedor"
      emptyTitle="Nenhum lançamento"
      emptyDescription="Ajuste os filtros ou registre um novo lançamento."
    />
  )
}
