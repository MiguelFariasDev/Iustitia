'use client'

import { Check } from 'lucide-react'
import Link from 'next/link'
import { alertTypeMeta } from '@/features/alertas/components/AlertaCard'
import type { Alerta } from '@/features/alertas/types'
import { routes } from '@/config/routes'
import { formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'

interface NotificationItemProps {
  alerta: Alerta
  onMarkAsRead: (id: string) => void
  /** Fecha o popover ao navegar. */
  onNavigate: () => void
}

/**
 * Um alerta no sino. O corpo é um link para o detalhe (que marca como lido ao abrir); o botão
 * de "marcar como lida" é IRMÃO do link, não filho — botão dentro de link é HTML inválido e
 * quebra a navegação por teclado e por leitor de tela.
 */
export function NotificationItem({ alerta, onMarkAsRead, onNavigate }: NotificationItemProps) {
  const Icon = alertTypeMeta[alerta.type].icon

  return (
    <li className={cn('flex items-start gap-1 pr-2', !alerta.isRead && 'bg-secondary/5')}>
      <Link
        href={`${routes.alerts}/${alerta.id}` as never}
        onClick={onNavigate}
        className="flex min-w-0 flex-1 gap-3 px-4 py-3 outline-none hover:bg-accent focus-visible:bg-accent"
      >
        <Icon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
        <span className="min-w-0 flex-1 space-y-0.5">
          <span className={cn('block text-sm', !alerta.isRead && 'font-semibold')}>
            {alerta.title}
            {!alerta.isRead ? <span className="sr-only"> (não lido)</span> : null}
          </span>
          {alerta.description ? (
            <span className="block truncate text-xs text-muted-foreground">
              {alerta.description}
            </span>
          ) : null}
          <time dateTime={alerta.createdAt} className="block text-xs text-muted-foreground">
            {formatRelative(alerta.createdAt)}
          </time>
        </span>
      </Link>
      {!alerta.isRead ? (
        <button
          type="button"
          onClick={() => onMarkAsRead(alerta.id)}
          aria-label={`Marcar como lido: ${alerta.title}`}
          className="mt-3 flex size-7 shrink-0 items-center justify-center rounded-md text-muted-foreground outline-none hover:bg-accent hover:text-foreground focus-visible:ring-2 focus-visible:ring-ring"
        >
          <Check className="size-4" aria-hidden />
        </button>
      ) : null}
    </li>
  )
}
