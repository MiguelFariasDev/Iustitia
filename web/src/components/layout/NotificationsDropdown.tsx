'use client'

import { Bell, CheckCheck } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
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
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" className="relative" aria-label={label}>
          <Bell aria-hidden />
          {unread > 0 ? (
            <span
              aria-hidden
              className="absolute top-1 right-1 flex min-w-4 items-center justify-center rounded-full bg-ia-text px-1 font-mono text-[10px] leading-4 font-semibold text-background"
            >
              {unread > 9 ? '9+' : unread}
            </span>
          ) : null}
        </Button>
      </SheetTrigger>
      {/*
        Gaveta à direita (400px) com fundo escurecido: é um diálogo modal (foco preso, Esc e clique no
        fundo fecham). A entrada leva 0,25s (token --dur-layer) em vez dos 0,5s padrão da Sheet.
      */}
      <SheetContent
        side="right"
        className="w-full gap-0 p-0 data-[state=closed]:duration-150 data-[state=open]:duration-[250ms] sm:max-w-[400px]"
      >
        <div className="flex items-center justify-between gap-2 border-b py-3 pr-12 pl-4">
          <SheetTitle className="text-base">Notificações</SheetTitle>
          <SheetDescription className="sr-only">
            Últimos alertas do escritório. Fechar com Esc ou clicando fora.
          </SheetDescription>
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
          <ul className="flex-1 divide-y overflow-y-auto" aria-label="Últimos alertas">
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

        <div className="mt-auto border-t p-2">
          <Button variant="ghost" size="sm" className="w-full" asChild>
            <Link href={routes.alerts} onClick={() => setOpen(false)}>
              Ver todos
              <span className="sr-only"> os alertas</span>
            </Link>
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  )
}
