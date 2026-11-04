'use client'

import type { ColumnDef, SortingState } from '@tanstack/react-table'
import { Archive, CheckCheck, Download, Eye } from 'lucide-react'
import { useMemo } from 'react'
import { DataTable } from '@/components/shared/DataTable'
import { Button } from '@/components/ui/button'
import { formatDate } from '@/lib/format'
import { kindMeta } from '../presentation'
import type { BulkAction } from '../hooks'
import type { Publication, PublicationSort } from '../types'
import { PublicacaoEmptyState } from './PublicacaoEmptyState'
import { PublicacaoRowSummary } from './PublicacaoRow'
import { PublicacaoStatusBadge } from './PublicacaoStatusBadge'

interface PublicacaoListProps {
  items: Publication[]
  isLoading: boolean
  filtered: boolean
  onClearFilters: () => void
  page: number
  pageCount: number
  totalCount: number
  onPageChange: (page: number) => void
  ordem: PublicationSort
  onOrdemChange: (ordem: PublicationSort) => void
  selected: ReadonlySet<string>
  onSelectedChange: (next: Set<string>) => void
  onBulkAction: (action: BulkAction) => void
  bulkPending: boolean
  onExport: () => void
  exporting: boolean
}

/** Coluna ordenável ↔ valor de `ordem` da API. Cliente não ordena: o nome vive em outra tabela. */
function toSorting(ordem: PublicationSort): SortingState {
  switch (ordem) {
    case 'oldest':
      return [{ id: 'publishedAt', desc: false }]
    case 'tribunal':
      return [{ id: 'tribunal', desc: false }]
    case 'tribunal-desc':
      return [{ id: 'tribunal', desc: true }]
    default:
      return [{ id: 'publishedAt', desc: true }]
  }
}

function fromSorting(sorting: SortingState): PublicationSort {
  const first = sorting[0]
  if (!first) return 'recent'
  if (first.id === 'tribunal') return first.desc ? 'tribunal-desc' : 'tribunal'
  return first.desc ? 'recent' : 'oldest'
}

export function PublicacaoList({
  items,
  isLoading,
  filtered,
  onClearFilters,
  page,
  pageCount,
  totalCount,
  onPageChange,
  ordem,
  onOrdemChange,
  selected,
  onSelectedChange,
  onBulkAction,
  bulkPending,
  onExport,
  exporting,
}: PublicacaoListProps) {
  const columns = useMemo<ColumnDef<Publication, unknown>[]>(
    () => [
      {
        id: 'publishedAt',
        // accessorFn é o que torna a coluna ordenável no TanStack; a ordenação em si é do servidor.
        accessorFn: (row) => row.publishedAt,
        header: 'Divulgado em',
        cell: ({ row }) => (
          <time dateTime={row.original.publishedAt} className="whitespace-nowrap tabular">
            {formatDate(row.original.publishedAt)}
          </time>
        ),
      },
      {
        id: 'tipo',
        header: 'Tipo',
        enableSorting: false,
        cell: ({ row }) => {
          const kind = kindMeta[row.original.kind]
          const Icon = kind.icon
          return (
            <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
              <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
              {kind.label}
            </span>
          )
        },
      },
      {
        id: 'processo',
        header: 'Processo',
        enableSorting: false,
        cell: ({ row }) => <PublicacaoRowSummary item={row.original} />,
      },
      {
        id: 'tribunal',
        accessorFn: (row) => row.tribunal ?? '',
        header: 'Diário',
        cell: ({ row }) => (
          <span className="text-sm whitespace-nowrap text-muted-foreground">
            {row.original.tribunal ?? '—'} · {row.original.journal}
          </span>
        ),
      },
      {
        id: 'cliente',
        header: 'Cliente',
        enableSorting: false,
        cell: ({ row }) => (
          <span className="block max-w-48 truncate" title={row.original.clienteNome ?? undefined}>
            {row.original.clienteNome ?? '—'}
          </span>
        ),
      },
      {
        id: 'status',
        header: 'Status',
        enableSorting: false,
        cell: ({ row }) => <PublicacaoStatusBadge status={row.original.status} />,
      },
    ],
    []
  )

  const hasSelection = selected.size > 0

  const toolbar = (
    <>
      {hasSelection ? (
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Ações em massa">
          <span aria-live="polite" className="text-sm text-muted-foreground">
            {selected.size} selecionada(s)
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={bulkPending}
            onClick={() => onBulkAction('treated')}
          >
            <CheckCheck aria-hidden />
            Marcar como tratada
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={bulkPending}
            onClick={() => onBulkAction('read')}
          >
            <Eye aria-hidden />
            Marcar como lida
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={bulkPending}
            onClick={() => onBulkAction('archived')}
          >
            <Archive aria-hidden />
            Arquivar
          </Button>
        </div>
      ) : null}
      <Button
        variant="outline"
        size="sm"
        onClick={onExport}
        disabled={exporting || totalCount === 0}
      >
        <Download aria-hidden />
        {exporting ? 'Exportando…' : 'Exportar CSV'}
      </Button>
    </>
  )

  return (
    <DataTable
      columns={columns}
      data={items}
      getRowId={(row) => row.id}
      caption="Publicações capturadas dos diários oficiais, com data, tipo, processo, diário, cliente e situação de tratamento."
      enableSearch={false}
      isLoading={isLoading}
      toolbar={toolbar}
      selection={{
        selected,
        onChange: onSelectedChange,
        rowLabel: (row) =>
          `Selecionar publicação do processo ${row.processNumber ?? 'sem processo'} de ${formatDate(row.publishedAt)}`,
      }}
      serverPagination={{ page, pageCount, totalCount, onPageChange }}
      serverSorting={{
        value: toSorting(ordem),
        onChange: (next) => onOrdemChange(fromSorting(next)),
      }}
      emptyNode={<PublicacaoEmptyState filtered={filtered} onClear={onClearFilters} />}
    />
  )
}
