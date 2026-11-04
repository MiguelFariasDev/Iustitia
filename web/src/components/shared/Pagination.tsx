'use client'

import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { formatNumber } from '@/lib/format'
import { cn } from '@/lib/utils'

interface PaginationProps {
  /** Página atual (base 1, como PagedList da API). */
  page: number
  pageCount: number
  onPageChange: (page: number) => void
  totalCount?: number
  className?: string
}

export function Pagination({
  page,
  pageCount,
  onPageChange,
  totalCount,
  className,
}: PaginationProps) {
  const safeCount = Math.max(pageCount, 1)
  const canPrev = page > 1
  const canNext = page < safeCount

  return (
    <nav
      aria-label="Paginação"
      className={cn('flex flex-col items-center justify-between gap-3 sm:flex-row', className)}
    >
      <p className="text-sm text-muted-foreground" aria-live="polite">
        Página <span className="font-medium text-foreground tabular">{page}</span> de{' '}
        <span className="font-medium text-foreground tabular">{safeCount}</span>
        {totalCount !== undefined ? (
          <>
            {' '}
            · <span className="tabular">{formatNumber(totalCount)}</span>{' '}
            {totalCount === 1 ? 'registro' : 'registros'}
          </>
        ) : null}
      </p>
      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="icon-sm"
          onClick={() => onPageChange(1)}
          disabled={!canPrev}
          aria-label="Primeira página"
        >
          <ChevronsLeft aria-hidden />
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
          onClick={() => onPageChange(page - 1)}
          disabled={!canPrev}
          aria-label="Página anterior"
        >
          <ChevronLeft aria-hidden />
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
          onClick={() => onPageChange(page + 1)}
          disabled={!canNext}
          aria-label="Próxima página"
        >
          <ChevronRight aria-hidden />
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
          onClick={() => onPageChange(safeCount)}
          disabled={!canNext}
          aria-label="Última página"
        >
          <ChevronsRight aria-hidden />
        </Button>
      </div>
    </nav>
  )
}
