'use client'

import { BellOff } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { Pagination } from '@/components/shared/Pagination'
import { Skeleton } from '@/components/ui/skeleton'
import { tabLabels } from '../tabs'
import type { AlertaTab, AlertasPage } from '../types'
import { AlertaCard } from './AlertaCard'

interface AlertaListProps {
  tab: AlertaTab
  data: AlertasPage | undefined
  isLoading: boolean
  filtered: boolean
  onPageChange: (page: number) => void
  onMarkAsRead: (id: string) => void
  onDismiss: (id: string) => void
  busy: boolean
}

const emptyMessages: Record<AlertaTab, string> = {
  important: 'Nenhum alerta importante no momento',
  all: 'Você ainda não recebeu alertas',
  publications: 'Nenhuma publicação gerou alerta',
  deadlines: 'Nenhum alerta de prazo por aqui',
}

export function AlertaList({
  tab,
  data,
  isLoading,
  filtered,
  onPageChange,
  onMarkAsRead,
  onDismiss,
  busy,
}: AlertaListProps) {
  if (isLoading) {
    return (
      <div className="space-y-3" role="status" aria-label="Carregando alertas">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-24 rounded-xl" />
        ))}
        <span className="sr-only">Carregando alertas…</span>
      </div>
    )
  }

  if (!data || data.items.length === 0) {
    return (
      <EmptyState
        icon={BellOff}
        title={filtered ? 'Nenhum alerta corresponde aos filtros' : emptyMessages[tab]}
        description={
          filtered
            ? 'Ajuste ou limpe os filtros para ver mais alertas.'
            : 'Quando chegar uma publicação nova, ou algo precisar da sua atenção, ela aparece aqui.'
        }
      />
    )
  }

  return (
    <div className="space-y-4">
      <ul aria-label={tabLabels[tab]} className="space-y-3">
        {data.items.map((alerta) => (
          <li key={alerta.id}>
            <AlertaCard
              alerta={alerta}
              onMarkAsRead={onMarkAsRead}
              onDismiss={onDismiss}
              disabled={busy}
            />
          </li>
        ))}
      </ul>
      {data.totalPages > 1 ? (
        <Pagination
          page={data.page}
          pageCount={data.totalPages}
          totalCount={data.totalCount}
          onPageChange={onPageChange}
        />
      ) : null}
    </div>
  )
}
