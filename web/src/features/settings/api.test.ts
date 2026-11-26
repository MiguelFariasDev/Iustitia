import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'

const envMock = vi.hoisted(() => ({ env: { useMocks: false } }))

vi.mock('@/config/env', () => envMock)
vi.mock('@/lib/api/client', () => ({ apiClient: { get: vi.fn(), put: vi.fn() } }))

import { updateProfile, updateTenantProfile } from './api'

const perfil = { name: 'Ana', email: 'ana@x.com', phone: '11999999999', oabNumber: '', oabState: '' as const }
const escritorio = {
  name: 'Escritório Novo',
  cnpj: '',
  oabState: 'SP' as const,
  email: '',
  phone: '',
  address: '',
  city: '',
  timezone: '',
}

beforeEach(() => {
  vi.mocked(apiClient.get).mockReset()
  vi.mocked(apiClient.put).mockReset()
  vi.mocked(apiClient.put).mockResolvedValue({ data: null })
  vi.mocked(apiClient.get).mockImplementation(async (url: string) => {
    if (url === endpoints.status.me) return { data: { userId: 'u1', tenantId: 't1', email: 'ana@x.com' } }
    if (url === endpoints.users.byId('u1')) return { data: { name: 'Ana', email: 'ana@x.com', phone: '' } }
    return { data: { name: 'Escritório', cnpj: '' } }
  })
})

describe('salvar perfil e escritório', () => {
  it('salvar o perfil não manda especialidade nem avatar (o servidor os mantém)', async () => {
    await updateProfile(perfil)

    const [, corpo] = vi.mocked(apiClient.put).mock.calls[0]!
    expect(corpo).toEqual({ name: 'Ana', phone: '11999999999' })
    expect(corpo).not.toHaveProperty('specialty')
    expect(corpo).not.toHaveProperty('avatarUrl')
  })

  it('renomear o escritório não manda logo nem endereço (o servidor os mantém)', async () => {
    await updateTenantProfile(escritorio)

    const [, corpo] = vi.mocked(apiClient.put).mock.calls[0]!
    expect(corpo).toEqual({ name: 'Escritório Novo' })
    expect(corpo).not.toHaveProperty('logoUrl')
    expect(corpo).not.toHaveProperty('addressJson')
  })
})
