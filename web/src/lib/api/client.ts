import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { env } from '@/config/env'
import { authService } from '@/lib/auth/authService'
import { ApiError, isProblemDetails } from './errors'

export const CORRELATION_HEADER = 'X-Correlation-Id'
export const IDEMPOTENCY_HEADER = 'Idempotency-Key'

/** POSTs que o backend protege com Idempotency-Key (IdempotencyMiddleware, ADR-041). */
const IDEMPOTENT_PATH_SUFFIXES = ['/tenants', '/users/invite', '/users/accept-invite']

function needsIdempotencyKey(config: InternalAxiosRequestConfig) {
  if (config.method?.toLowerCase() !== 'post') return false
  const path = (config.url ?? '').split('?')[0]!.replace(/\/$/, '')
  return IDEMPOTENT_PATH_SUFFIXES.some((suffix) => path.endsWith(suffix))
}

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean }

export interface ApiClientOptions {
  baseURL?: string
  getAccessToken?: () => Promise<string | null>
  refreshAccessToken?: () => Promise<string | null>
  onUnauthorized?: () => void
}

function redirectToLogin() {
  if (typeof window === 'undefined') return
  const next = `${window.location.pathname}${window.location.search}`
  // Navegação "dura" intencional: descarta todo estado em memória após perda de sessão.
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination
  window.location.assign(`/login?next=${encodeURIComponent(next)}`)
}

/**
 * Fábrica do cliente HTTP da API .NET. Exportada separadamente para testes; o app usa
 * `apiClient` abaixo. Interceptors:
 *  - request: Bearer do Supabase + X-Correlation-Id (ADR-037)
 *  - response 401: tenta renovar o token uma vez; se falhar, encerra a sessão
 *  - response erro: normaliza ProblemDetails (RFC 7807) em ApiError
 */
export function createApiClient(options: ApiClientOptions = {}): AxiosInstance {
  const {
    baseURL = env.apiUrl,
    getAccessToken = async () => (await authService.getSession())?.accessToken ?? null,
    refreshAccessToken = async () => (await authService.refreshSession())?.accessToken ?? null,
    onUnauthorized = () => {
      void authService.signOut().finally(redirectToLogin)
    },
  } = options

  const instance = axios.create({
    baseURL,
    timeout: 30_000,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
  })

  // Um único refresh em voo: 401s concorrentes aguardam a mesma renovação.
  let refreshing: Promise<string | null> | null = null
  function refreshOnce(): Promise<string | null> {
    refreshing ??= refreshAccessToken().finally(() => {
      refreshing = null
    })
    return refreshing
  }

  instance.interceptors.request.use(async (config) => {
    // No retry pós-refresh o header já carrega o token novo; sobrescrever aqui o
    // devolveria ao token expirado. Um Authorization explícito também tem precedência.
    if (!config.headers.has('Authorization')) {
      const token = await getAccessToken()
      if (token) config.headers.set('Authorization', `Bearer ${token}`)
    }
    if (!config.headers.has(CORRELATION_HEADER)) {
      config.headers.set(CORRELATION_HEADER, crypto.randomUUID())
    }
    // Gerada uma vez por requisição e mantida no retry pós-refresh (o header já está no
    // config), então reenviar o mesmo envio não duplica tenant nem convite.
    if (needsIdempotencyKey(config) && !config.headers.has(IDEMPOTENCY_HEADER)) {
      config.headers.set(IDEMPOTENCY_HEADER, crypto.randomUUID())
    }
    return config
  })

  instance.interceptors.response.use(
    (response) => response,
    async (error: unknown) => {
      if (!(error instanceof AxiosError)) throw error

      const config = error.config as RetriableConfig | undefined
      const status = error.response?.status ?? (error.code === 'ECONNABORTED' ? 408 : 0)

      if (status === 401 && config && !config._retried) {
        config._retried = true
        const token = await refreshOnce()
        if (token) {
          config.headers.set('Authorization', `Bearer ${token}`)
          return instance.request(config)
        }
        onUnauthorized()
      }

      const data: unknown = error.response?.data
      throw new ApiError(status, isProblemDetails(data) ? data : {})
    }
  )

  return instance
}

export const apiClient = createApiClient()
