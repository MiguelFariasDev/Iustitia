'use client'

import Link from 'next/link'
import { useEffect, useRef } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { useAlerta, useMarkAlertaAsRead } from '../hooks'
import { AlertaCard } from './AlertaCard'

/**
 * Destino do clique no alerta (painel, sino, toast). Abre o alerta, marca como lido e oferece
 * ir para a publicação — o alerta é o aviso, a publicação é onde o advogado age.
 */
export function AlertaDetail({ alertaId }: { alertaId: string }) {
  const { data, isPending, isError, error, refetch } = useAlerta(alertaId)
  const markAsRead = useMarkAlertaAsRead()
  const autoMarked = useRef(false)

  useEffect(() => {
    if (data && !data.isRead && !autoMarked.current) {
      autoMarked.current = true
      markAsRead.mutate(data.id)
    }
  }, [data, markAsRead])

  if (isPending) {
    return <Skeleton className="h-28 rounded-xl" role="status" aria-label="Carregando alerta" />
  }

  if (isError || !data) {
    return (
      <ErrorState
        error={error}
        onRetry={() => void refetch()}
        title="Não foi possível carregar o alerta"
      />
    )
  }

  return (
    <div className="space-y-4">
      <AlertaCard
        alerta={data}
        onMarkAsRead={() => undefined}
        onDismiss={() => undefined}
        disabled
      />
      {data.type === 'Publication' && data.referenceId ? (
        <Button asChild>
          <Link href={`${routes.publications}/${data.referenceId}` as never}>
            Abrir a publicação
          </Link>
        </Button>
      ) : null}
    </div>
  )
}
