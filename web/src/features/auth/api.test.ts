vi.mock('@/config/env', () => ({ env: { useMocks: false }, isMockAuth: false }))

const post = vi.fn()
vi.mock('@/lib/api/client', () => ({
  apiClient: { post: (...args: unknown[]) => post(...args) },
}))

import { createTenant, type OnboardingInput } from './api'

const base: OnboardingInput = {
  office: {
    accountType: 'Firm',
    officeName: 'Souza & Associados',
    cnpj: '11.444.777/0001-61',
    cpf: '',
    oabState: 'CE',
    phone: '(85) 99999-1234',
  },
  owner: {
    ownerName: 'Ana Souza',
    email: 'ana@souza.adv.br',
    oabNumber: '123.456',
    password: 'Senha123!',
    confirmPassword: 'Senha123!',
  },
  preferences: {
    teamSize: '2-5',
    practiceAreas: ['Cível'],
    acceptTerms: true,
    acknowledgeAiReview: true,
  },
}

const response = { tenantId: 't1', adminUserId: 'u1', requiresEmailConfirmation: false }

beforeEach(() => {
  post.mockReset()
  post.mockResolvedValue({ data: response })
})

describe('createTenant — contrato com POST /tenants', () => {
  it('envia o formato do CreateTenantCommand do backend (admin*), não o do formulário', async () => {
    await createTenant(base)

    const [url, body] = post.mock.calls[0]!
    expect(url).toBe('/tenants')
    expect(body).toEqual({
      accountType: 'Firm',
      name: 'Souza & Associados',
      cnpj: '11444777000161',
      cpf: null,
      adminName: 'Ana Souza',
      adminEmail: 'ana@souza.adv.br',
      adminPassword: 'Senha123!',
      adminOabNumber: '123456',
      adminOabState: 'CE',
      adminPhone: '85999991234',
    })
  })

  it('não envia campos que o backend desconhece (ownerName, ownerEmail, equipe, áreas)', async () => {
    await createTenant(base)

    const body = post.mock.calls[0]![1] as Record<string, unknown>
    for (const campo of [
      'ownerName',
      'ownerEmail',
      'ownerOabNumber',
      'teamSize',
      'practiceAreas',
      'oabState',
    ]) {
      expect(body).not.toHaveProperty(campo)
    }
  })

  it('conta solo leva o nome do titular, o CPF e nenhum CNPJ', async () => {
    await createTenant({
      ...base,
      office: {
        ...base.office,
        accountType: 'Solo',
        officeName: '',
        cnpj: '',
        cpf: '529.982.247-25',
      },
    })

    expect(post.mock.calls[0]![1]).toMatchObject({
      accountType: 'Solo',
      name: 'Ana Souza',
      cnpj: null,
      cpf: '52998224725',
    })
  })

  it('ignora o CNPJ digitado antes de a pessoa trocar para solo', async () => {
    await createTenant({
      ...base,
      office: {
        ...base.office,
        accountType: 'Solo',
        cnpj: '11.444.777/0001-61',
        cpf: '529.982.247-25',
      },
    })

    expect(post.mock.calls[0]![1]).toMatchObject({ cnpj: null })
  })

  it('sem número de OAB não manda a seccional (o backend só aceita as duas juntas)', async () => {
    await createTenant({ ...base, owner: { ...base.owner, oabNumber: '' } })

    expect(post.mock.calls[0]![1]).toMatchObject({ adminOabNumber: null, adminOabState: null })
  })

  it('campos opcionais vazios viram null', async () => {
    await createTenant({
      ...base,
      office: { ...base.office, cnpj: '', phone: '' },
      owner: { ...base.owner, oabNumber: '' },
    })

    expect(post.mock.calls[0]![1]).toMatchObject({ cnpj: null, adminPhone: null })
  })

  it('devolve a resposta do backend, inclusive requiresEmailConfirmation', async () => {
    post.mockResolvedValue({ data: { ...response, requiresEmailConfirmation: true } })

    await expect(createTenant(base)).resolves.toMatchObject({ requiresEmailConfirmation: true })
  })
})
