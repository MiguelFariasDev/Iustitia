import { screen } from '@testing-library/react'
import { routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { MOCK_EXPIRED_INVITE } from '../api'
import { InviteForm } from './InviteForm'

describe('InviteForm', () => {
  it('mostra os detalhes do convite (escritório, papel, quem convidou)', async () => {
    renderWithProviders(<InviteForm token="abc" />)
    expect(await screen.findByText('Souza & Associados Advocacia')).toBeInTheDocument()
    expect(screen.getByText('Advogado(a)')).toBeInTheDocument()
    expect(screen.getByText('Ana Souza')).toBeInTheDocument()
  })

  it('convite expirado mostra estado de erro com saída para o login', async () => {
    renderWithProviders(<InviteForm token={MOCK_EXPIRED_INVITE} />)
    expect(await screen.findByText(/Este convite expirou/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ir para o login' })).toHaveAttribute('href', '/login')
  })

  it('exige aceite dos termos e, ao aceitar, leva ao login', async () => {
    const { user } = renderWithProviders(<InviteForm token="abc" />)
    await user.type(await screen.findByLabelText('Nome completo'), 'Bruno Lima')
    await user.type(screen.getByLabelText('Senha'), 'SenhaForte1')
    await user.type(screen.getByLabelText('Confirmar senha'), 'SenhaForte1')
    await user.click(screen.getByRole('button', { name: 'Aceitar convite' }))
    expect(await screen.findByText('É necessário aceitar os termos de uso.')).toBeInTheDocument()

    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Aceitar convite' }))
    await vi.waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith('/login'))
  })
})
