import { screen, waitFor } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { renderWithProviders } from '@/test/render'
import * as api from '../api'
import { OnboardingWizard } from './OnboardingWizard'

const { replace, signIn } = vi.hoisted(() => ({ replace: vi.fn(), signIn: vi.fn() }))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace, refresh: vi.fn(), push: vi.fn() }),
}))
vi.mock('@/lib/auth/authService', () => ({ authService: { signIn } }))
vi.mock('../api')

const created = { tenantId: 't1', adminUserId: 'u1', requiresEmailConfirmation: false }

beforeEach(() => {
  vi.mocked(api.createTenant).mockResolvedValue(created)
  signIn.mockResolvedValue({
    accessToken: 'tok',
    expiresAt: null,
    user: { ...mockUser, email: 'ana@souza.adv.br' },
  })
})

afterEach(() => {
  vi.resetAllMocks()
  useAuthStore.setState({ user: null, session: null, isAuthenticated: false })
})

describe('OnboardingWizard — tipo de conta', () => {
  it('começa como escritório, com nome e CNPJ', () => {
    renderWithProviders(<OnboardingWizard />)

    expect(screen.getByRole('heading', { name: /Etapa 1 de 3: Escritório/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Nome do escritório')).toBeInTheDocument()
    expect(screen.getByLabelText('CNPJ (opcional)')).toBeInTheDocument()
    expect(screen.queryByLabelText('CPF')).toBeNull()
  })

  it('oferece as duas opções de conta', () => {
    renderWithProviders(<OnboardingWizard />)

    const grupo = screen.getByRole('radiogroup', { name: 'Como você atua?' })
    expect(grupo).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /Escritório/ })).toBeChecked()
    expect(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ })).not.toBeChecked()
  })

  it('escolher autônomo troca nome e CNPJ por CPF e renomeia a etapa', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))

    expect(screen.queryByLabelText('Nome do escritório')).toBeNull()
    expect(screen.queryByLabelText('CNPJ (opcional)')).toBeNull()
    expect(screen.getByLabelText('CPF')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: /Etapa 1 de 3: Dados profissionais/ })
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Crie sua conta' })).toBeInTheDocument()
  })

  it('autônomo não avança sem CPF', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByText('Informe o seu CPF.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Etapa 1 de 3/ })).toBeInTheDocument()
  })

  it('máscara o CPF enquanto digita', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))
    await user.type(screen.getByLabelText('CPF'), '52998224725')

    expect(screen.getByLabelText('CPF')).toHaveValue('529.982.247-25')
  })
})

async function escolherOab(user: UserEvent) {
  await user.click(screen.getByRole('combobox', { name: 'Seccional da OAB' }))
  await user.click(await screen.findByRole('option', { name: 'OAB/CE' }))
}

async function preencherTitular(user: UserEvent) {
  await user.type(screen.getByLabelText('Seu nome completo'), 'Ana Souza')
  await user.type(screen.getByLabelText('E-mail'), 'ana@souza.adv.br')
  await user.type(screen.getByLabelText('Senha'), 'Senha123!')
  await user.type(screen.getByLabelText('Confirmar senha'), 'Senha123!')
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
}

async function concluirPreferencias(user: UserEvent, { equipe }: { equipe: boolean }) {
  if (equipe) {
    await user.click(await screen.findByRole('combobox', { name: 'Tamanho da equipe' }))
    await user.click(await screen.findByRole('option', { name: '2 a 5 pessoas' }))
  }
  await user.click(screen.getByLabelText('Cível'))
  await user.click(screen.getByLabelText(/Estou ciente de que toda sugestão de IA/))
  await user.click(screen.getByLabelText(/Li e aceito os termos de uso/))
  await user.click(screen.getByRole('button', { name: /Criar (escritório|conta)/ }))
}

async function cadastrarEscritorio(user: UserEvent) {
  await user.type(screen.getByLabelText('Nome do escritório'), 'Souza & Associados')
  await escolherOab(user)
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  await preencherTitular(user)
  await concluirPreferencias(user, { equipe: true })
}

describe('OnboardingWizard — cadastro de ponta a ponta (backend cria a conta, depois entra)', () => {
  it('cadastra uma única vez no backend e entra com a senha recém-definida', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await cadastrarEscritorio(user)

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/painel'))
    expect(api.createTenant).toHaveBeenCalledTimes(1)
    expect(api.createTenant).toHaveBeenCalledWith(
      expect.objectContaining({
        office: expect.objectContaining({
          accountType: 'Firm',
          officeName: 'Souza & Associados',
          oabState: 'CE',
        }),
        owner: expect.objectContaining({ email: 'ana@souza.adv.br', password: 'Senha123!' }),
      })
    )
    expect(signIn).toHaveBeenCalledWith({ email: 'ana@souza.adv.br', password: 'Senha123!' })
    expect(useAuthStore.getState().session?.accessToken).toBe('tok')
  })

  it('só entra DEPOIS de o cadastro terminar', async () => {
    const ordem: string[] = []
    vi.mocked(api.createTenant).mockImplementation(async () => {
      ordem.push('cadastro')
      return created
    })
    signIn.mockImplementation(async () => {
      ordem.push('login')
      return { accessToken: 'tok', expiresAt: null, user: mockUser }
    })
    const { user } = renderWithProviders(<OnboardingWizard />)

    await cadastrarEscritorio(user)

    await waitFor(() => expect(replace).toHaveBeenCalled())
    expect(ordem).toEqual(['cadastro', 'login'])
  })

  it('com confirmação de e-mail exigida, não tenta entrar e orienta a confirmar', async () => {
    vi.mocked(api.createTenant).mockResolvedValue({ ...created, requiresEmailConfirmation: true })
    const { user } = renderWithProviders(<OnboardingWizard />)

    await cadastrarEscritorio(user)

    expect(await screen.findByRole('heading', { name: 'Confirme seu e-mail' })).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('ana@souza.adv.br')
    expect(screen.getByRole('link', { name: 'Ir para o login' })).toHaveAttribute('href', '/login')
    expect(signIn).not.toHaveBeenCalled()
    expect(replace).not.toHaveBeenCalled()
  })

  it('se a entrada automática falhar, avisa que a conta existe e NÃO manda repetir o cadastro', async () => {
    signIn.mockRejectedValue(new Error('rede'))
    const { user } = renderWithProviders(<OnboardingWizard />)

    await cadastrarEscritorio(user)

    expect(await screen.findByRole('heading', { name: 'Conta criada' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).toBeNull()
    expect(screen.getByRole('link', { name: 'Ir para o login' })).toBeInTheDocument()
    expect(api.createTenant).toHaveBeenCalledTimes(1)
  })

  it('se o cadastro falhar, mostra o erro, não tenta entrar e deixa tentar de novo', async () => {
    vi.mocked(api.createTenant).mockRejectedValue(new Error('Já existe uma conta com este e-mail.'))
    const { user } = renderWithProviders(<OnboardingWizard />)

    await cadastrarEscritorio(user)

    // A etapa tem outro alerta (o aviso sobre IA): o do erro é o que carrega a mensagem.
    expect(await screen.findByText('Já existe uma conta com este e-mail.')).toBeInTheDocument()
    expect(signIn).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Criar escritório' })).toBeEnabled()
  })

  it('advogado autônomo: manda CPF, não pergunta equipe e o botão diz "Criar conta"', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))
    await user.type(screen.getByLabelText('CPF'), '52998224725')
    await escolherOab(user)
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await preencherTitular(user)

    expect(screen.queryByRole('combobox', { name: 'Tamanho da equipe' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
    await concluirPreferencias(user, { equipe: false })

    await waitFor(() => expect(replace).toHaveBeenCalledWith('/painel'))
    expect(api.createTenant).toHaveBeenCalledWith(
      expect.objectContaining({
        office: expect.objectContaining({ accountType: 'Solo', cpf: '529.982.247-25' }),
        preferences: expect.objectContaining({ teamSize: '1' }),
      })
    )
  })
})
