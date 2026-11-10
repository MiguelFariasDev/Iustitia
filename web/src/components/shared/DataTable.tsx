'use client'
'use no memo' // TanStack Table não é compatível com a memoização do React Compiler.

import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table'
import { ArrowDown, ArrowUp, ArrowUpDown, Inbox } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Checkbox } from '@/components/ui/checkbox'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { cn } from '@/lib/utils'
import { EmptyState } from './EmptyState'
import { Pagination } from './Pagination'
import { SearchInput } from './SearchInput'

interface DataTableProps<TData> {
  columns: ColumnDef<TData, unknown>[]
  data: TData[]
  /** Legenda acessível da tabela (lida por leitores de tela). */
  caption: string
  searchPlaceholder?: string
  enableSearch?: boolean
  pageSize?: number
  isLoading?: boolean
  toolbar?: React.ReactNode
  emptyTitle?: string
  emptyDescription?: string
  getRowId?: (row: TData) => string
  /**
   * Seleção múltipla (controlada por quem usa). Exige `getRowId`. O cabeçalho seleciona/limpa
   * só as linhas da PÁGINA visível — "selecionar tudo" que alcança páginas não carregadas
   * esconde de quem clica o que de fato vai ser alterado.
   */
  selection?: {
    selected: ReadonlySet<string>
    onChange: (next: Set<string>) => void
    /** Nome acessível do checkbox de uma linha (ex.: "Selecionar publicação de 12/10"). */
    rowLabel: (row: TData) => string
  }
  /**
   * Paginação feita no SERVIDOR: `data` já é a página atual e `page` é base 1 (como PagedList
   * da API). Desliga a paginação e o filtro locais.
   */
  serverPagination?: {
    page: number
    pageCount: number
    totalCount: number
    onPageChange: (page: number) => void
  }
  /** Conteúdo próprio do estado vazio (substitui o EmptyState padrão). */
  emptyNode?: React.ReactNode
  /** Ordenação feita no servidor: a tabela só reflete e emite o estado, não reordena. */
  serverSorting?: {
    value: SortingState
    onChange: (next: SortingState) => void
  }
}

/**
 * Tabela com ordenação, filtro global e paginação no cliente. Quando os endpoints
 * paginados existirem, a mesma API receberá `manualPagination` + PagedList da API.
 */
export function DataTable<TData>({
  columns,
  data,
  caption,
  searchPlaceholder = 'Filtrar…',
  enableSearch = true,
  pageSize = 10,
  isLoading = false,
  toolbar,
  emptyTitle = 'Nenhum registro encontrado',
  emptyDescription,
  getRowId,
  selection,
  serverPagination,
  serverSorting,
  emptyNode,
}: DataTableProps<TData>) {
  const [localSorting, setLocalSorting] = useState<SortingState>([])
  const [globalFilter, setGlobalFilter] = useState('')
  const sorting = serverSorting?.value ?? localSorting

  // A coluna de seleção precisa ter identidade ESTÁVEL entre renders: o TanStack trata
  // `header`/`cell` (funções) como componentes, e uma função nova a cada render faria o React
  // desmontar e remontar a tabela inteira a cada clique — perdendo o foco do teclado. A seleção
  // atual chega às células por `table.options.meta`, que é relido a cada render.
  const hasSelection = Boolean(selection)
  const allColumns = useMemo(
    () => (hasSelection ? [selectionColumn<TData>(), ...columns] : columns),
    [hasSelection, columns]
  )

  const table = useReactTable({
    data,
    columns: allColumns,
    state: { sorting, globalFilter },
    onSortingChange: (updater) => {
      const next = typeof updater === 'function' ? updater(sorting) : updater
      if (serverSorting) serverSorting.onChange(next)
      else setLocalSorting(next)
    },
    onGlobalFilterChange: setGlobalFilter,
    getRowId,
    meta: { selection } satisfies TableMeta<TData>,
    // Ordenação no servidor alterna só entre crescente e decrescente: um terceiro estado
    // "sem ordem" não tem significado para a API (ela sempre ordena por algo).
    enableSortingRemoval: !serverSorting,
    manualPagination: Boolean(serverPagination),
    manualSorting: Boolean(serverSorting),
    manualFiltering: Boolean(serverPagination),
    pageCount: serverPagination?.pageCount,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: serverSorting ? undefined : getSortedRowModel(),
    // Primeiro clique sempre crescente, inclusive em colunas numéricas (o padrão do
    // TanStack as inverte): afordância previsível e igual em toda a tabela.
    sortDescFirst: false,
    getFilteredRowModel: serverPagination ? undefined : getFilteredRowModel(),
    getPaginationRowModel: serverPagination ? undefined : getPaginationRowModel(),
    initialState: { pagination: { pageSize } },
  })

  const rows = table.getRowModel().rows
  const { pageIndex } = table.getState().pagination

  return (
    <div className="space-y-4">
      {enableSearch || toolbar ? (
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          {enableSearch ? (
            <SearchInput
              onSearch={(value) => {
                setGlobalFilter(value)
                table.setPageIndex(0)
              }}
              placeholder={searchPlaceholder}
              label={searchPlaceholder}
            />
          ) : (
            <span />
          )}
          {toolbar ? <div className="flex items-center gap-2">{toolbar}</div> : null}
        </div>
      ) : null}

      <div className="overflow-x-auto rounded-xl border bg-card" aria-busy={isLoading}>
        {/* Anuncia o carregamento: os esqueletos sozinhos são invisíveis para leitor de tela. */}
        {isLoading ? (
          <span role="status" aria-label="Carregando tabela" className="sr-only">
            Carregando…
          </span>
        ) : null}
        <Table>
          <caption className="sr-only">{caption}</caption>
          <TableHeader className="bg-muted/50">
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  const sorted = header.column.getIsSorted()
                  const canSort = header.column.getCanSort()
                  return (
                    <TableHead
                      key={header.id}
                      aria-sort={
                        sorted === 'asc'
                          ? 'ascending'
                          : sorted === 'desc'
                            ? 'descending'
                            : undefined
                      }
                    >
                      {header.isPlaceholder ? null : canSort ? (
                        <button
                          type="button"
                          onClick={header.column.getToggleSortingHandler()}
                          className="-ml-2 inline-flex items-center gap-1 rounded px-2 py-1 hover:bg-accent"
                        >
                          {flexRender(header.column.columnDef.header, header.getContext())}
                          {sorted === 'asc' ? (
                            <ArrowUp className="size-3.5" aria-hidden />
                          ) : sorted === 'desc' ? (
                            <ArrowDown className="size-3.5" aria-hidden />
                          ) : (
                            <ArrowUpDown className="size-3.5 opacity-50" aria-hidden />
                          )}
                        </button>
                      ) : (
                        flexRender(header.column.columnDef.header, header.getContext())
                      )}
                    </TableHead>
                  )
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 5 }, (_, i) => (
                <TableRow key={`skeleton-${i}`}>
                  {allColumns.map((_, j) => (
                    <TableCell key={j}>
                      <Skeleton className="h-4 w-full max-w-40" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : rows.length > 0 ? (
              rows.map((row) => (
                <TableRow key={row.id}>
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={allColumns.length} className="p-4">
                  {emptyNode ?? (
                    <EmptyState
                      icon={Inbox}
                      title={emptyTitle}
                      description={emptyDescription}
                      className="border-0"
                    />
                  )}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      {!isLoading && serverPagination && serverPagination.pageCount > 1 ? (
        <Pagination
          page={serverPagination.page}
          pageCount={serverPagination.pageCount}
          totalCount={serverPagination.totalCount}
          onPageChange={serverPagination.onPageChange}
          className="px-1"
        />
      ) : null}

      {!isLoading && !serverPagination && table.getPageCount() > 1 ? (
        <Pagination
          page={pageIndex + 1}
          pageCount={table.getPageCount()}
          totalCount={table.getFilteredRowModel().rows.length}
          onPageChange={(page) => table.setPageIndex(page - 1)}
          className={cn('px-1')}
        />
      ) : null}
    </div>
  )
}

type Selection<TData> = NonNullable<DataTableProps<TData>['selection']>

interface TableMeta<TData> {
  selection: Selection<TData> | undefined
}

/**
 * Coluna de checkbox. Estado derivado das linhas visíveis: "tudo marcado" é calculado a cada
 * render a partir do Set (não duplicado em outro estado), com o estado intermediário quando só
 * parte da página está marcada. Lê a seleção de `table.options.meta` — ver o comentário em
 * DataTable sobre a identidade estável desta definição.
 */
function selectionColumn<TData>(): ColumnDef<TData, unknown> {
  const read = (table: { options: { meta?: unknown } }) =>
    (table.options.meta as TableMeta<TData>).selection!

  return {
    id: '__select',
    enableSorting: false,
    header: ({ table }) => {
      const selection = read(table)
      const ids = table.getRowModel().rows.map((row) => row.id)
      const marked = ids.filter((id) => selection.selected.has(id)).length
      const all = ids.length > 0 && marked === ids.length

      return (
        <Checkbox
          checked={all ? true : marked > 0 ? 'indeterminate' : false}
          onCheckedChange={() => {
            const next = new Set(selection.selected)
            for (const id of ids) {
              if (all) next.delete(id)
              else next.add(id)
            }
            selection.onChange(next)
          }}
          aria-label="Selecionar todas as linhas desta página"
        />
      )
    },
    cell: ({ row, table }) => {
      const selection = read(table)

      return (
        <Checkbox
          checked={selection.selected.has(row.id)}
          onCheckedChange={() => {
            const next = new Set(selection.selected)
            if (!next.delete(row.id)) next.add(row.id)
            selection.onChange(next)
          }}
          aria-label={selection.rowLabel(row.original)}
        />
      )
    },
  }
}
