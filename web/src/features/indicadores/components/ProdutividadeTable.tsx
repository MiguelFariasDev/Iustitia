'use client'

import type { ColumnDef } from '@tanstack/react-table'
import { DataTable } from '@/components/shared/DataTable'
import { formatNumber } from '@/lib/format'
import type { Produtividade } from '../types'

const numero = (valor: number) => <span className="tabular-nums">{formatNumber(valor)}</span>

const colunas: ColumnDef<Produtividade, unknown>[] = [
  { accessorKey: 'nome', header: 'Advogado' },
  {
    accessorKey: 'processosAtivos',
    header: 'Processos ativos',
    cell: ({ row }) => numero(row.original.processosAtivos),
  },
  {
    accessorKey: 'tarefasPendentes',
    header: 'Tarefas pendentes',
    cell: ({ row }) => numero(row.original.tarefasPendentes),
  },
  {
    accessorKey: 'tarefasConcluidas',
    header: 'Tarefas concluídas',
    cell: ({ row }) => numero(row.original.tarefasConcluidas),
  },
]

export function ProdutividadeTable({ dados }: { dados: Produtividade[] }) {
  return (
    <DataTable
      caption="Produtividade por advogado"
      columns={colunas}
      data={dados}
      getRowId={(p) => p.id}
      enableSearch={false}
      emptyTitle="Sem dados de produtividade"
    />
  )
}
