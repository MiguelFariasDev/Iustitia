import { apiClient } from '@/lib/api/client'
import { getCurrentTenant } from './api'

const envMock = vi.hoisted(() => ({ useMocks: false }))
vi.mock('@/config/env', () => ({ env: envMock, isMockAuth: false }))
vi.mock('@/lib/api/client', () => ({ apiClient: { get: vi.fn() } }))

const get = vi.mocked(apiClient.get)

function responder(rotas: Record<string, unknown | Error>) {
  get.mockImplementation(async (url: string) => {
    const resposta = rotas[url]
    if (resposta instanceof Error) throw resposta
    if (resposta === undefined) throw new Error(`rota inesperada: ${url}`)

    return { data: resposta } as never
  })
}

beforeEach(() => {
  get.mockReset()
})

describe('getCurrentTenant (API real)', () => {
  it('monta o contexto a partir de /me, do escritório, das flags e das configurações', async () => {
    responder({
      '/me': { tenantId: 't1' },
      '/tenants/t1': { id: 't1', name: 'Souza & Associados' },
      '/feature-flags': {
        items: [
          { key: 'ai.classification', isEnabled: true },
          { key: 'legal.module', isEnabled: false },
        ],
      },
      '/settings': { items: [{ key: 'locale', valueJson: '"en-US"' }] },
    })

    const contexto = await getCurrentTenant()

    expect(contexto.tenant).toMatchObject({
      id: 't1',
      name: 'Souza & Associados',
      slug: 'souza-associados',
    })
    expect(contexto.featureFlags).toEqual({ 'ai.classification': true, 'legal.module': false })
    expect(contexto.settings.locale).toBe('en-US')
    expect(contexto.settings.timezone).toBe('America/Sao_Paulo')
  })

  it('escritório sem flags nem configurações: devolve flags vazias (nunca undefined)', async () => {
    responder({
      '/me': { tenantId: 't1' },
      '/tenants/t1': { id: 't1', name: 'Escritório' },
      '/feature-flags': { items: [] },
      '/settings': { items: [] },
    })

    expect((await getCurrentTenant()).featureFlags).toEqual({})
  })

  it('uma consulta que falha não derruba o contexto', async () => {
    responder({
      '/me': { tenantId: 't1' },
      '/tenants/t1': new Error('403'),
      '/feature-flags': new Error('403'),
      '/settings': new Error('500'),
    })

    const contexto = await getCurrentTenant()

    expect(contexto.featureFlags).toEqual({})
    expect(contexto.tenant.id).toBe('t1')
    expect(contexto.settings.locale).toBe('pt-BR')
  })

  it('sessão sem escritório não consulta o escritório', async () => {
    responder({
      '/me': { tenantId: null },
      '/feature-flags': { items: [] },
      '/settings': { items: [] },
    })

    const contexto = await getCurrentTenant()

    expect(contexto.tenant.id).toBe('')
    expect(get).not.toHaveBeenCalledWith(expect.stringContaining('/tenants/'))
  })
})
