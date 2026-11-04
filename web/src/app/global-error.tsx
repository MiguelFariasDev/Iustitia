'use client'

import { useEffect } from 'react'
import { reportError } from '@/lib/observability/reportError'
import './globals.css'

/** Último recurso: erro no próprio root layout. Sem providers disponíveis aqui. */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    reportError(error, { digest: error.digest, scope: 'global' })
  }, [error])

  return (
    <html lang="pt-BR">
      <body className="flex min-h-dvh items-center justify-center p-6">
        <main className="max-w-md space-y-4 text-center">
          <h1 className="text-xl font-semibold">Algo deu errado</h1>
          <p className="text-sm text-muted-foreground">
            Ocorreu um erro inesperado. Tente novamente; se persistir, contate o suporte
            {error.digest ? ` informando o código ${error.digest}` : ''}.
          </p>
          <button
            type="button"
            onClick={reset}
            className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground"
          >
            Tentar novamente
          </button>
        </main>
      </body>
    </html>
  )
}
