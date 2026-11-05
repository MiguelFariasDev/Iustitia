import { screen } from '@testing-library/react'
import { MOCK_MFA_CODE, MOCK_PASSWORD } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { navigationState, routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { LoginForm } from './LoginForm'

describe('LoginForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clear()
    sessionStorage.clear()
    localStorage.clear()
  })

  it('valida campos obrigatórios antes de enviar', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('Informe o e-mail.')).toBeInTheDocument()
    expect(screen.getByText('Informe a senha.')).toBeInTheDocument()
    expect(screen.getByLabelText('E-mail')).toHaveAttribute('aria-invalid', 'true')
  })

  it('mostra erro de credenciais inválidas em um alerta', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), 'errada')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha incorretos.')
    expect(routerMock.replace).not.toHaveBeenCalled()
  })

  it('autentica e redireciona para o destino seguro de ?next=', async () => {
    navigationState.searchParams = new URLSearchParams('next=/configuracoes/usuarios')
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), MOCK_PASSWORD)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    await vi.waitFor(() =>
      expect(routerMock.replace).toHaveBeenCalledWith('/configuracoes/usuarios')
    )
    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })

  it('ignora ?next= externo (open redirect)', async () => {
    navigationState.searchParams = new URLSearchParams('next=https://evil.com')
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), MOCK_PASSWORD)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    await vi.waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith('/painel'))
  })

  it('permite mostrar/ocultar a senha', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    const input = screen.getByLabelText('Senha')
    expect(input).toHaveAttribute('type', 'password')
    await user.click(screen.getByRole('button', { name: 'Mostrar senha' }))
    expect(input).toHaveAttribute('type', 'text')
  })

  it('exige o código de 2 fatores quando a conta tem 2FA e só então redireciona', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana.2fa@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), MOCK_PASSWORD)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Verificação em duas etapas' })).toBeInTheDocument()
    expect(routerMock.replace).not.toHaveBeenCalled()

    await user.type(screen.getByLabelText('Código de verificação'), MOCK_MFA_CODE)
    await vi.waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith('/painel'))
  })

  it('código de 2FA errado mostra erro e não entra', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana.2fa@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), MOCK_PASSWORD)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    await user.type(await screen.findByLabelText('Código de verificação'), '000000')

    expect(await screen.findByRole('alert')).toHaveTextContent(/inválido/i)
    expect(routerMock.replace).not.toHaveBeenCalled()
  })

  it('bloqueia o botão com contagem regressiva depois de 5 falhas', async () => {
    const { user } = renderWithProviders(<LoginForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana@escritorio.com.br')
    await user.type(screen.getByLabelText('Senha'), 'errada')
    for (let i = 0; i < 5; i++) {
      await user.click(screen.getByRole('button', { name: 'Entrar' }))
      await screen.findByRole('alert')
    }
    expect(await screen.findByText(/aguarde \d+s/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeDisabled()
  })
})
