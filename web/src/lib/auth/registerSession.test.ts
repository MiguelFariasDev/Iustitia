import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'

const envMock = vi.hoisted(() => ({
  env: { useMocks: false },
  isMockAuth: false,
}))

vi.mock('@/config/env', () => envMock)
vi.mock('@/lib/api/client', () => ({ apiClient: { post: vi.fn() } }))

import { registerSession } from './registerSession'

beforeEach(() => {
  envMock.env.useMocks = false
  envMock.isMockAuth = false
  vi.mocked(apiClient.post).mockReset()
})

describe('registerSession', () => {
  it('avisa a API do login com sessão Supabase real', async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: null })

    await registerSession()

    expect(apiClient.post).toHaveBeenCalledWith(endpoints.auth.session)
  })

  it('não chama a API no modo mock', async () => {
    envMock.isMockAuth = true

    await registerSession()

    expect(apiClient.post).not.toHaveBeenCalled()
  })

  it('falha da API não impede a entrada', async () => {
    vi.mocked(apiClient.post).mockRejectedValue(new Error('rede'))

    await expect(registerSession()).resolves.toBeUndefined()
  })
})
