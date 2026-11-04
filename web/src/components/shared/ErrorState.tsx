'use client'

import { ErrorFallback } from './ErrorFallback'

interface ErrorStateProps {
  error: unknown
  /** Normalmente o `refetch` da query: o botão "Tentar novamente" refaz só ela. */
  onRetry?: () => void
  title?: string
  className?: string
}

/**
 * Estado de erro de uma query (não de render): mensagem amigável, código de suporte e retry.
 * Existe ao lado do ErrorBoundary porque `useQuery` (sem suspense) devolve o erro como valor —
 * não lança — e a tela precisa decidir o que mostrar no lugar dos dados.
 */
export function ErrorState({ error, onRetry, title, className }: ErrorStateProps) {
  return <ErrorFallback error={error} onRetry={onRetry} title={title} className={className} />
}
