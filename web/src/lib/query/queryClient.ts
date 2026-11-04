import { isServer, QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api/errors'

function shouldRetry(failureCount: number, error: unknown) {
  // Erros 4xx são determinísticos: repetir não ajuda.
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
  return failureCount < 2
}

export function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60_000,
        gcTime: 5 * 60_000,
        retry: shouldRetry,
        refetchOnWindowFocus: true,
      },
      mutations: {
        retry: false,
      },
    },
  })
}

let browserQueryClient: QueryClient | undefined

/**
 * No servidor, sempre um cliente novo (nunca compartilhar cache entre usuários).
 * No navegador, um singleton — evita recriar o cache se o React suspender na 1ª renderização.
 */
export function getQueryClient() {
  if (isServer) return makeQueryClient()
  browserQueryClient ??= makeQueryClient()
  return browserQueryClient
}
