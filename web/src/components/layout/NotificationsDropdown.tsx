'use client'

import { Bell, CheckCheck } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { Skeleton } from '@/components/ui/skeleton'
import {
  useAlertas,
  useAlertasNaoLidos,
  useMarkAlertaAsRead,
  useMarkAllAlertasAsRead,
} from '@/features/alertas/hooks'
import { routes } from '@/config/routes'
import { NotificationItem } from './NotificationItem'

/** Quantos alertas o sino mostra; o resto fica em "Ver todos". */
const LATEST = 5

/**
 * Sino da barra superior: contador de não lidos (resumo, atualizado em tempo real pelo
 * Realtime e por polling de segurança) e os últimos alertas. A lista só é buscada com o
 * popover ABERTO — a barra aparece em todas as telas e não deve pagar uma consulta de lista
 * a cada navegação por causa de um menu que quase ninguém abre.
 */
export function NotificationsDropdown() {
  const [open, setOpen] = useState(false)
  const unread = useAlertasNaoLidos() ?? 0
  const latest = useAlertas({ page: 1, pageSize: LATEST }, { enabled: open })
  const markAsRead = useMarkAlertaAsRead()
  const markAll = useMarkAllAlertasAsRead()

  const label = unread > 0 ? `Notificações (${unread} não lidas)` : 'Notificações'

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className="relative" aria-label={label}>
          <Bell aria-hidden />
          {unread > 0 ? (
            <span
              aria-hidden
              className="absolute top-1 right-1 flex min-w-4 items-center justify-center rounded-full bg-red-700 px-1 text-[10px] leading-4 font-semibold text-white"
            >
              {unread > 9 ? '9+' : unread}
            </span>
          ) : null}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(24rem,calc(100vw-2rem))] p-0">
        <div className="flex items-center justify-between border-b px-4 py-3">
          <h2 className="text-sm font-semibold">Notificações</h2>
          <Button
            variant="ghost"
            size="xs"
            onClick={() => markAll.mutate()}
            disabled={unread === 0 || markAll.isPending}
            className="text-muted-foreground"
          >
            <CheckCheck aria-hidden />
            Marcar todas como lidas
          </Button>
        </div>

        {latest.isPending ? (
          <div className="space-y-2 p-4" role="status" aria-label="Carregando notificações">
            <Skeleton className="h-12 rounded-md" />
            <Skeleton className="h-12 rounded-md" />
            <span className="sr-only">Carregando notificações…</span>
          </div>
        ) : latest.isError ? (
          <ErrorState
            error={latest.error}
            onRetry={() => void latest.refetch()}
            title="Não foi possível carregar"
            className="m-4"
          />
        ) : latest.data.items.length === 0 ? (
          <EmptyState
            icon={Bell}
            title="Tudo em dia"
            description="Você não tem notificações."
            className="m-4"
          />
        ) : (
          <ul className="max-h-96 divide-y overflow-y-auto" aria-label="Últimos alertas">
            {latest.data.items.map((alerta) => (
              <NotificationItem
                key={alerta.id}
                alerta={alerta}
                onMarkAsRead={(id) => markAsRead.mutate(id)}
                onNavigate={() => setOpen(false)}
              />
            ))}
          </ul>
        )}

        <div className="border-t p-2">
          <Button variant="ghost" size="sm" className="w-full" asChild>
            <Link href={routes.alerts} onClick={() => setOpen(false)}>
              Ver todos
              <span className="sr-only"> os alertas</span>
            </Link>
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  )
}
