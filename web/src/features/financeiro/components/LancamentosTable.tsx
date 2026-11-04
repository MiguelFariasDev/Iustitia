'use client'

import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { formatCurrency, formatDate } from '@/lib/format'
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

/**
 * Colunas por aba. Em nível de módulo (identidade estável): o DataTable recria a tabela
 * inteira se `columns` mudar de referência a cada render, e perderia ordenação e foco.
 */
const colunasPorTipo: Record<TipoLancamento, Coluna[]> = {
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
  ],
  ContaReceber: [
    descricao,
    { accessorKey: 'cliente', header: 'Cliente', cell: ({ row }) => traco(row.original.cliente) },
    valor,
    vencimento,
    status,
  ],
}

interface LancamentosTableProps {
  tipo: TipoLancamento
  lancamentos: Lancamento[]
  isLoading?: boolean
}

export function LancamentosTable({ tipo, lancamentos, isLoading }: LancamentosTableProps) {
  const rotulo = tipoLancamentoLabels[tipo]

  return (
    <DataTable
      caption={`${rotulo}s`}
      columns={colunasPorTipo[tipo]}
      data={lancamentos.filter((l) => l.tipo === tipo)}
      isLoading={isLoading}
      getRowId={(l) => l.id}
      searchPlaceholder="Buscar por descrição, cliente ou fornecedor"
      emptyTitle="Nenhum lançamento"
      emptyDescription="Ajuste os filtros ou registre um novo lançamento."
    />
  )
}
