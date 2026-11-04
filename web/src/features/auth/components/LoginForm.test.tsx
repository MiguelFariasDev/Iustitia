import { screen } from '@testing-library/react'
import { MOCK_PASSWORD } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { navigationState, routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { LoginForm } from './LoginForm'

describe('LoginForm', () => {
  beforeEach(() => useAuthStore.getState().clear())

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
    expect(await screen.findByRole('alert')).toHaveTextContent('E-mail ou senha inválidos.')
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
})
