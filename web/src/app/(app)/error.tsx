'use client'

import { useEffect } from 'react'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { reportError } from '@/lib/observability/reportError'

export default function AppError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    reportError(error, { digest: error.digest })
  }, [error])

  return (
    <ErrorFallback
      error={error}
      onRetry={reset}
      title="Não foi possível carregar esta página"
      className="my-10"
    />
  )
}
