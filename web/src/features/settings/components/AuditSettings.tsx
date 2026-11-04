'use client'

import { useQuery } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { ShieldCheck } from 'lucide-react'
import { DataTable } from '@/components/shared/DataTable'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { formatDateTime } from '@/lib/format'
import { queryKeys } from '@/lib/query/queryKeys'
import { listAuditEntries } from '../api'
import type { AuditEntry } from '../types'

const columns: ColumnDef<AuditEntry, unknown>[] = [
  {
    accessorKey: 'occurredAt',
    header: 'Data/hora',
    sortingFn: 'datetime',
    cell: ({ row }) => (
      <time dateTime={row.original.occurredAt} className="whitespace-nowrap tabular">
        {formatDateTime(row.original.occurredAt)}
      </time>
    ),
  },
  { accessorKey: 'actor', header: 'Autor' },
  { accessorKey: 'action', header: 'Ação' },
  { accessorKey: 'entity', header: 'Recurso' },
  {
    accessorKey: 'ipAddress',
    header: 'IP',
    enableSorting: false,
    cell: ({ row }) => <span className="font-mono text-xs">{row.original.ipAddress}</span>,
  },
]

export function AuditSettings() {
  const query = useQuery({ queryKey: queryKeys.audit.list(), queryFn: listAuditEntries })
  if (query.isError)
    return <ErrorFallback error={query.error} onRetry={() => void query.refetch()} />

  return (
    <div className="space-y-4">
      <Alert>
        <ShieldCheck aria-hidden />
        <AlertDescription>
          Registros de auditoria são imutáveis e retidos por no mínimo 5 anos. Nenhum registro pode
          ser editado ou excluído.
        </AlertDescription>
      </Alert>
      <DataTable
        caption="Registro de auditoria"
        columns={columns}
        data={query.data ?? []}
        isLoading={query.isPending}
        getRowId={(e) => e.id}
        searchPlaceholder="Filtrar por autor, ação ou recurso"
        pageSize={10}
      />
    </div>
  )
}
