'use client'

import { QueryErrorResetBoundary } from '@tanstack/react-query'
import { Suspense } from 'react'
import { ErrorBoundary } from './ErrorBoundary'

interface QueryBoundaryProps {
  children: React.ReactNode
  fallback: React.ReactNode
  errorTitle?: string
}

/**
 * Suspense + ErrorBoundary para componentes com useSuspenseQuery. "Tentar novamente"
 * também reseta o erro da query, forçando um novo fetch.
 */
export function QueryBoundary({ children, fallback, errorTitle }: QueryBoundaryProps) {
  return (
    <QueryErrorResetBoundary>
      {({ reset }) => (
        <ErrorBoundary onReset={reset} title={errorTitle}>
          <Suspense fallback={fallback}>{children}</Suspense>
        </ErrorBoundary>
      )}
    </QueryErrorResetBoundary>
  )
}
