'use client'

import { useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { useAlertaSummary, useAlertas, useDismissAlerta, useMarkAlertaAsRead } from '../hooks'
import { tabFilters } from '../tabs'
import { alertaTabs, type AlertaTab, type AlertasFilters } from '../types'
import { AlertaFilters } from './AlertaFilters'
import { AlertaList } from './AlertaList'
import { AlertaTabs } from './AlertaTabs'

export function AlertasView() {
  const searchParams = useSearchParams()

  // A aba inicial pode vir da URL (?aba=publications|deadlines), para o painel e o sino levarem direto
  // à lista certa. Só o estado INICIAL: depois, quem manda são as abas da tela.
  const [tab, setTab] = useState<AlertaTab>(() => {
    const fromUrl = searchParams.get('aba') as AlertaTab | null
    return fromUrl && alertaTabs.includes(fromUrl) ? fromUrl : 'important'
  })
  const [extra, setExtra] = useState<AlertasFilters>({ page: 1 })

  // O preset da aba tem precedência sobre tipo/leitura do filtro manual (que nem aparecem
  // fora da aba "Todos"); o período vale sempre.
  const filters: AlertasFilters = {
    page: extra.page ?? 1,
    from: extra.from ?? null,
    to: extra.to ?? null,
    ...(tab === 'all' ? { types: extra.types, isRead: extra.isRead ?? null } : tabFilters(tab)),
  }

  const { data, isPending, isError, error, refetch } = useAlertas(filters)
  const { data: summary } = useAlertaSummary()
  const markAsRead = useMarkAlertaAsRead()
  const dismiss = useDismissAlerta()

  const updateFilters = (patch: Partial<AlertasFilters>) =>
    setExtra((current) => ({ ...current, ...patch, page: 1 }))

  const filtered =
    Boolean(extra.from || extra.to) ||
    (tab === 'all' && (Boolean(extra.types?.length) || (extra.isRead ?? null) !== null))

  return (
    <div className="space-y-4">
      <AlertaFilters
        tab={tab}
        filters={extra}
        onChange={updateFilters}
        onClear={() => setExtra({ page: 1 })}
      />

      <AlertaTabs
        value={tab}
        onChange={(next) => {
          setTab(next)
          setExtra((current) => ({ ...current, page: 1 }))
        }}
        summary={summary}
      >
        {isError ? (
          <ErrorState
            error={error}
            onRetry={() => void refetch()}
            title="Não foi possível carregar os alertas"
          />
        ) : (
          <AlertaList
            tab={tab}
            data={data}
            isLoading={isPending}
            filtered={filtered}
            onPageChange={(page) => setExtra((current) => ({ ...current, page }))}
            onMarkAsRead={(id) => markAsRead.mutate(id)}
            onDismiss={(id) => dismiss.mutate(id)}
            busy={markAsRead.isPending || dismiss.isPending}
          />
        )}
      </AlertaTabs>
    </div>
  )
}
