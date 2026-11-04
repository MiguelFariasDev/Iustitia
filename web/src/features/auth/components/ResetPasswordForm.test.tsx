import { screen } from '@testing-library/react'
import { routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { ResetPasswordForm } from './ResetPasswordForm'

describe('ResetPasswordForm', () => {
  it('exige senha forte e confirmação igual', async () => {
    const { user } = renderWithProviders(<ResetPasswordForm />)
    await user.type(screen.getByLabelText('Nova senha'), 'fraca')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'outra')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))
    expect(await screen.findByText('A senha deve ter pelo menos 8 caracteres.')).toBeInTheDocument()
    expect(screen.getByText('As senhas não conferem.')).toBeInTheDocument()
  })

  it('indica em tempo real os requisitos atendidos', async () => {
    const { user } = renderWithProviders(<ResetPasswordForm />)
    await user.type(screen.getByLabelText('Nova senha'), 'Abcdefgh')
    const rules = screen.getByRole('list', { name: 'Requisitos da senha' })
    expect(rules).toHaveTextContent('Pelo menos 8 caracteres (atendido)')
    expect(rules).toHaveTextContent('Um número (pendente)')
  })

  it('salva e redireciona para o painel', async () => {
    const { user } = renderWithProviders(<ResetPasswordForm />)
    await user.type(screen.getByLabelText('Nova senha'), 'NovaSenha1')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'NovaSenha1')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))
    await vi.waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith('/painel'))
  })
})
