'use client'

import { Bell, Cake, ClipboardList, Newspaper, X, type LucideIcon } from 'lucide-react'
import Link from 'next/link'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { formatDateTime, formatRelative } from '@/lib/format'
import { cn } from '@/lib/utils'
import type { Alerta, AlertType } from '../types'

export const alertTypeMeta: Record<AlertType, { label: string; icon: LucideIcon }> = {
  Publication: { label: 'Publicação', icon: Newspaper },
  Task: { label: 'Tarefa', icon: ClipboardList },
  Birthday: { label: 'Aniversário', icon: Cake },
  Other: { label: 'Outros', icon: Bell },
}

interface AlertaCardProps {
  alerta: Alerta
  onMarkAsRead: (id: string) => void
  onDismiss: (id: string) => void
  disabled?: boolean
}

export function AlertaCard({ alerta, onMarkAsRead, onDismiss, disabled }: AlertaCardProps) {
  const meta = alertTypeMeta[alerta.type]
  const Icon = meta.icon

  return (
    <article
      aria-labelledby={`alerta-${alerta.id}-titulo`}
      className={cn(
        'flex gap-3 rounded-xl border bg-card p-4',
        !alerta.isRead && 'border-secondary/40 bg-secondary/5'
      )}
    >
      <span className="mt-0.5 flex size-9 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
        <Icon className="size-4" aria-hidden />
      </span>

      <div className="min-w-0 flex-1 space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <h3
            id={`alerta-${alerta.id}-titulo`}
            className={cn('text-sm', !alerta.isRead && 'font-semibold')}
          >
            {alerta.title}
          </h3>
          {!alerta.isRead ? (
            <StatusBadge tone="brand" hideDot>
              Nova
            </StatusBadge>
          ) : null}
          <StatusBadge tone="neutral" hideDot>
            {meta.label}
          </StatusBadge>
        </div>

        {alerta.description ? (
          <p className="text-sm text-muted-foreground">{alerta.description}</p>
        ) : null}

        <p className="text-xs text-muted-foreground">
          <time dateTime={alerta.createdAt} title={formatDateTime(alerta.createdAt)}>
            {formatRelative(alerta.createdAt)}
          </time>
        </p>

        <div className="flex flex-wrap items-center gap-2 pt-1">
          {alerta.type === 'Publication' && alerta.referenceId ? (
            <Button
              size="sm"
              variant="outline"
              asChild
              // Abrir a publicação a partir do alerta já conta como "visto".
              onClick={() => (alerta.isRead ? undefined : onMarkAsRead(alerta.id))}
            >
              <Link href={`${routes.publications}/${alerta.referenceId}` as never}>
                Ver publicação
                <span className="sr-only"> — {alerta.title}</span>
              </Link>
            </Button>
          ) : null}
          {!alerta.isRead ? (
            <Button
              size="sm"
              variant="ghost"
              disabled={disabled}
              onClick={() => onMarkAsRead(alerta.id)}
            >
              Marcar como lida
              <span className="sr-only">: {alerta.title}</span>
            </Button>
          ) : null}
        </div>
      </div>

      <Button
        size="icon-sm"
        variant="ghost"
        disabled={disabled}
        onClick={() => onDismiss(alerta.id)}
        aria-label={`Descartar alerta: ${alerta.title}`}
        className="shrink-0 text-muted-foreground"
      >
        <X aria-hidden />
      </Button>
    </article>
  )
}
