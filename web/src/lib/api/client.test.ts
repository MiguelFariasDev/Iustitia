import { AxiosError, AxiosHeaders, type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios'
import { CORRELATION_HEADER, createApiClient } from './client'
import { ApiError } from './errors'

function respond(config: InternalAxiosRequestConfig, status: number, data: unknown) {
  const response = { data, status, statusText: String(status), headers: {}, config }
  if (status >= 400) {
    return Promise.reject(
      new AxiosError(`HTTP ${status}`, 'ERR_BAD_RESPONSE', config, null, response)
    )
  }
  return Promise.resolve(response)
}

describe('apiClient', () => {
  it('envia Bearer token e X-Correlation-Id em toda requisição', async () => {
    const seen: AxiosHeaders[] = []
    const adapter: AxiosAdapter = (config) => {
      seen.push(AxiosHeaders.from(config.headers))
      return respond(config, 200, { ok: true })
    }
    const client = createApiClient({ getAccessToken: async () => 'jwt-123' })
    client.defaults.adapter = adapter

    await client.get('/me')

    expect(seen[0]?.get('Authorization')).toBe('Bearer jwt-123')
    expect(seen[0]?.get(CORRELATION_HEADER)).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('normaliza ProblemDetails em ApiError', async () => {
    const client = createApiClient({ getAccessToken: async () => null })
    client.defaults.adapter = (config) =>
      respond(config, 404, {
        title: 'TENANT_NOT_FOUND',
        detail: 'Escritório não encontrado.',
        errorCode: 'TENANT_NOT_FOUND',
        correlationId: 'corr-1',
      })

    const error = await client.get('/tenants/x').catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 404,
      code: 'TENANT_NOT_FOUND',
      message: 'Escritório não encontrado.',
      correlationId: 'corr-1',
    })
  })

  it('em 401 renova o token uma vez e repete a requisição', async () => {
    let calls = 0
    const client = createApiClient({
      getAccessToken: async () => 'expirado',
      refreshAccessToken: async () => 'novo',
      onUnauthorized: vi.fn(),
    })
    client.defaults.adapter = (config) => {
      calls += 1
      const auth = AxiosHeaders.from(config.headers).get('Authorization')
      return auth === 'Bearer novo' ? respond(config, 200, { ok: true }) : respond(config, 401, {})
    }

    const { data } = await client.get<{ ok: boolean }>('/me')
    expect(data.ok).toBe(true)
    expect(calls).toBe(2)
  })

  it('em 401 sem refresh possível, encerra a sessão (onUnauthorized)', async () => {
    const onUnauthorized = vi.fn()
    const client = createApiClient({
      getAccessToken: async () => 'expirado',
      refreshAccessToken: async () => null,
      onUnauthorized,
    })
    client.defaults.adapter = (config) => respond(config, 401, {})

    await expect(client.get('/me')).rejects.toMatchObject({ status: 401 })
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('erro de rede vira ApiError status 0 com mensagem amigável', async () => {
    const client = createApiClient({ getAccessToken: async () => null })
    client.defaults.adapter = (config) =>
      Promise.reject(new AxiosError('Network Error', 'ERR_NETWORK', config))
    const error = (await client.get('/me').catch((e: unknown) => e)) as ApiError
    expect(error.isNetworkError).toBe(true)
    expect(error.message).toMatch(/conectar ao servidor/)
  })
})
