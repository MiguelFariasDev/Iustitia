import { screen, waitFor } from '@testing-library/react'
import type { AuthUser } from '@/lib/auth/types'
import { useAuthStore } from '@/stores/authStore'
import { navigationState } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { useOnboardingStore } from '../store'
import { OnboardingExperience } from './OnboardingExperience'

const owner: AuthUser = {
  id: 'u-owner', email: 'ana@souza.adv.br', name: 'Ana Souza', avatarUrl: null, role: 'Owner', tenantId: 't-1',
}
const lawyer: AuthUser = { ...owner, id: 'u-law', email: 'bruno@souza.adv.br', name: 'Bruno Lima', role: 'Lawyer' }

function signInAs(user: AuthUser) {
  useAuthStore.getState().setSession({ accessToken: 't', expiresAt: null, user })
}

const heading = (name: string) => screen.findByRole('heading', { name })

describe('OnboardingExperience', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    useOnboardingStore.getState().clear()
    useAuthStore.getState().clear()
    navigationState.searchParams = new URLSearchParams()
  })

  it('Sócio começa no passo 01/06 e o primeiro passo NÃO pode ser adiado', async () => {
    signInAs(owner)
    renderWithProviders(<OnboardingExperience />)

    expect(await heading('Seu escritório')).toBeInTheDocument()
    expect(screen.getByLabelText('Passo 1 de 6')).toHaveTextContent('01 / 06')
    expect(screen.queryByRole('button', { name: 'Fazer depois' })).not.toBeInTheDocument()
  })

  it('valida o nome do escritório antes de avançar', async () => {
    signInAs(owner)
    const { user } = renderWithProviders(<OnboardingExperience />)
    await heading('Seu escritório')

    await user.clear(screen.getByLabelText('Nome do escritório'))
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByText('Informe o nome do escritório.')).toBeInTheDocument()
  })

  it('exige o aceite dos termos quando ele não veio do cadastro (outro dispositivo)', async () => {
    signInAs(owner)
    const { user } = renderWithProviders(<OnboardingExperience />)
    await heading('Seu escritório')

    await user.type(screen.getByLabelText('Nome do escritório'), 'Souza & Associados')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByText('É necessário aceitar os termos para continuar.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Seu escritório' })).toBeInTheDocument()
  })

  it('avança, SALVA o progresso e retoma do passo seguinte ao recarregar', async () => {
    signInAs(owner)
    const first = renderWithProviders(<OnboardingExperience />)
    await heading('Seu escritório')
    await first.user.type(screen.getByLabelText('Nome do escritório'), 'Souza & Associados')
    await first.user.click(screen.getByRole('checkbox'))
    await first.user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await heading('O que a Athena deve monitorar')).toBeInTheDocument()
    first.unmount()

    // "Recarregar": estado em memória descartado, o que vale é o que foi persistido.
    useOnboardingStore.getState().clear()
    renderWithProviders(<OnboardingExperience />)

    expect(await heading('O que a Athena deve monitorar')).toBeInTheDocument()
    expect(screen.getByLabelText('Passo 2 de 6')).toBeInTheDocument()
  })

  it('"Fazer depois" adia o passo e avança; o passo adiado continua pendente', async () => {
    signInAs(owner)
    const seeded = renderWithProviders(<OnboardingExperience />)
    await heading('Seu escritório')
    await seeded.user.type(screen.getByLabelText('Nome do escritório'), 'Souza & Associados')
    await seeded.user.click(screen.getByRole('checkbox'))
    await seeded.user.click(screen.getByRole('button', { name: 'Continuar' }))
    await heading('O que a Athena deve monitorar')

    await seeded.user.click(screen.getByRole('button', { name: 'Fazer depois' }))

    expect(await heading('Como a Athena deve trabalhar')).toBeInTheDocument()
    const state = useOnboardingStore.getState().state!
    expect(state.skippedSteps).toEqual(['monitoring'])
    expect(state.completedSteps).toEqual(['office'])
  })

  it('a revisão humana aparece TRAVADA (sempre ativa) no passo da Athena', async () => {
    signInAs(owner)
    navigationState.searchParams = new URLSearchParams('passo=athena')
    renderWithProviders(<OnboardingExperience />)

    expect(await heading('Como a Athena deve trabalhar')).toBeInTheDocument()
    expect(screen.getByText('Revisão humana: sempre ativa')).toBeInTheDocument()
    expect(screen.getByRole('switch', { name: /Revisão humana obrigatória/ })).toBeDisabled()
    expect(screen.getByRole('switch', { name: /Revisão humana obrigatória/ })).toBeChecked()
  })

  it('?passo= inválido é ignorado (cai no primeiro pendente)', async () => {
    signInAs(owner)
    navigationState.searchParams = new URLSearchParams('passo=../../etc')
    renderWithProviders(<OnboardingExperience />)

    expect(await heading('Seu escritório')).toBeInTheDocument()
  })

  it('convidado (Advogado) faz o onboarding CURTO de 2 passos', async () => {
    signInAs(lawyer)
    const { user } = renderWithProviders(<OnboardingExperience />)

    expect(await heading('Seu perfil')).toBeInTheDocument()
    expect(screen.getByLabelText('Passo 1 de 2')).toHaveTextContent('01 / 02')
    expect(screen.getByLabelText('Como devemos te chamar?')).toHaveValue('Bruno Lima')

    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await heading('O que importa para você')).toBeInTheDocument()
    expect(screen.getByText('Revisão da IA')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ir para o painel' }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Configuração concluída' })).toBeInTheDocument())
  })

  it('progresso do convidado fica no usuário, separado do escritório', async () => {
    signInAs(lawyer)
    const { user } = renderWithProviders(<OnboardingExperience />)
    await heading('Seu perfil')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await heading('O que importa para você')

    expect(localStorage.getItem('iu-onboarding:user:u-law')).toContain('"completedSteps":["profile"]')
    expect(localStorage.getItem('iu-onboarding:office:t-1')).toBeNull()
  })
})
