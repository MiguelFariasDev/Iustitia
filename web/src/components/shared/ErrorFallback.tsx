'use client'

import { AlertTriangle, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ApiError } from '@/lib/api/errors'
import { cn } from '@/lib/utils'

interface ErrorFallbackProps {
  error: unknown
  onRetry?: () => void
  title?: string
  className?: string
}

/** Fallback amigável: nunca expõe stack trace; mostra o correlation id para o suporte. */
export function ErrorFallback({
  error,
  onRetry,
  title = 'Não foi possível carregar esta seção',
  className,
}: ErrorFallbackProps) {
  const correlationId = error instanceof ApiError ? error.correlationId : undefined
  const message =
    error instanceof ApiError
      ? error.message
      : 'Ocorreu um erro inesperado. Tente novamente em instantes.'

  return (
    <div
      role="alert"
      className={cn(
        'flex flex-col items-center gap-3 rounded-xl border border-destructive/30 bg-destructive/5 px-6 py-10 text-center',
        className
      )}
    >
      <AlertTriangle className="size-8 text-destructive" aria-hidden />
      <div className="max-w-md space-y-1">
        <h2 className="text-base font-semibold">{title}</h2>
        <p className="text-sm text-muted-foreground">{message}</p>
        {correlationId ? (
          <p className="font-mono text-xs text-muted-foreground">
            Código de suporte: {correlationId}
          </p>
        ) : null}
      </div>
      {onRetry ? (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RotateCcw aria-hidden />
          Tentar novamente
        </Button>
      ) : null}
    </div>
  )
}
