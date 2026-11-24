'use client'

import { useMutation } from '@tanstack/react-query'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useCallback, useMemo, useState } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { downloadCsv } from '@/lib/csv'
import { handleApiError } from '@/lib/api/toastHandler'
import { notify } from '@/lib/notify'
import { exportPublicationsCsv } from '../export'
import { countActiveFilters, filtersFromSearchParams, filtersToSearchParams } from '../filters'
import { useBulkPublicacaoAction, usePublicacoes, type BulkAction } from '../hooks'
import type { PublicationSort, PublicationsFilters } from '../types'
import { quickViewFilters, quickViewOf, quickViews } from '../view'
import { PublicacaoFilters } from './PublicacaoFilters'
import { PublicacaoList } from './PublicacaoList'
import { PublicacaoResumoCards } from './PublicacaoResumoCards'

/**
 * Tela de publicações. O estado dos filtros mora na URL (ver filters.ts): trocar um filtro é
 * uma navegação `replace` — sem empilhar histórico a cada tecla — e a página sobrevive a
 * recarga, ao botão voltar e ao compartilhamento do link.
 */
export function PublicacoesView() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()

  // searchParams é estável entre renders até a URL mudar; derivar daqui evita um estado
  // duplicado que poderia divergir da URL.
  const filters = useMemo(
    () => filtersFromSearchParams(new URLSearchParams(searchParams.toString())),
    [searchParams]
  )

  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set())

  const replace = useCallback(
    (next: PublicationsFilters) => {
      const query = filtersToSearchParams(next).toString()
      router.replace((query ? `${pathname}?${query}` : pathname) as never, { scroll: false })
      // A seleção vale para a página/consulta em que foi feita; carregar outra lista com
      // linhas marcadas que o usuário não vê levaria "tratar" a alterar publicações ocultas.
      setSelected(new Set())
    },
    [router, pathname]
  )

  const updateFilters = (patch: Partial<PublicationsFilters>) =>
    replace({ ...filters, ...patch, page: 1 })
  const clearFilters = () => replace({ ordem: filters.ordem })

  const { data, isPending, isError, error, refetch } = usePublicacoes(filters)
  const bulk = useBulkPublicacaoAction()

  const exportMutation = useMutation({
    mutationFn: () => exportPublicationsCsv(filters),
    onSuccess: (csv) => {
      downloadCsv(`publicacoes-${new Date().toISOString().slice(0, 10)}.csv`, csv)
      notify.success('Exportação concluída.')
    },
    onError: (e) => handleApiError(e),
  })

  const runBulk = (action: BulkAction) => {
    bulk.mutate({ action, ids: [...selected] }, { onSuccess: () => setSelected(new Set()) })
  }

  return (
    <div className="space-y-6">
      <PublicacaoResumoCards />

      <section aria-labelledby="publicacoes-lista" className="space-y-4">
        <h2 id="publicacoes-lista" className="sr-only">
          Lista de publicações
        </h2>

        <SegmentedControl
          value={quickViewOf(filters)}
          onChange={(view) => updateFilters(quickViewFilters(view))}
          options={quickViews}
          label="Visão da lista de publicações"
        />

        <PublicacaoFilters filters={filters} onChange={updateFilters} onClear={clearFilters} />

        {isError ? (
          <ErrorState
            error={error}
            onRetry={() => void refetch()}
            title="Não foi possível carregar as publicações"
          />
        ) : (
          <PublicacaoList
            items={data?.items ?? []}
            isLoading={isPending}
            filtered={countActiveFilters(filters) > 0}
            onClearFilters={clearFilters}
            page={data?.page ?? filters.page ?? 1}
            pageCount={data?.totalPages ?? 1}
            totalCount={data?.totalCount ?? 0}
            onPageChange={(page) => replace({ ...filters, page })}
            ordem={filters.ordem ?? 'recent'}
            onOrdemChange={(ordem: PublicationSort) => replace({ ...filters, ordem, page: 1 })}
            selected={selected}
            onSelectedChange={setSelected}
            onBulkAction={runBulk}
            bulkPending={bulk.isPending}
            onExport={() => exportMutation.mutate()}
            exporting={exportMutation.isPending}
          />
        )}
      </section>
    </div>
  )
}
