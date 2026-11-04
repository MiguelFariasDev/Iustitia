import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { ForgotPasswordForm } from './ForgotPasswordForm'

describe('ForgotPasswordForm', () => {
  it('rejeita e-mail inválido', async () => {
    const { user } = renderWithProviders(<ForgotPasswordForm />)
    await user.type(screen.getByLabelText('E-mail'), 'nao-e-email')
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(await screen.findByText('Informe um e-mail válido.')).toBeInTheDocument()
  })

  it('mostra confirmação genérica (sem revelar se a conta existe)', async () => {
    const { user } = renderWithProviders(<ForgotPasswordForm />)
    await user.type(screen.getByLabelText('E-mail'), 'ana@escritorio.com.br')
    await user.click(screen.getByRole('button', { name: 'Enviar link' }))
    expect(await screen.findByRole('status')).toHaveTextContent(
      /Se existir uma conta para ana@escritorio.com.br/
    )
    expect(screen.getByRole('link', { name: /voltar para o login/i })).toHaveAttribute(
      'href',
      '/login'
    )
  })
})
