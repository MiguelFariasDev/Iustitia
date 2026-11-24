import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { useAuthStore } from '@/stores/authStore'
import { AthenaLayer } from './AthenaLayer'

function loginAs(role: string) {
  useAuthStore.setState({
    user: {
      id: 'u1',
      email: 'a@b.c',
      name: 'Ana Souza',
      role,
      tenantId: 't1',
      accountType: 'Firm',
    },
  } as never)
}

afterEach(() => useAuthStore.setState({ user: null } as never))

describe('AthenaLayer', () => {
  it('não aparece para quem não acessa processos', () => {
    loginAs('Financial')
    renderWithProviders(<AthenaLayer />)

    expect(screen.queryByRole('button', { name: /Athena/ })).toBeNull()
  })

  it('abre o painel com saudação e responde sem fingir IA', async () => {
    loginAs('Lawyer')
    const { user } = renderWithProviders(<AthenaLayer />)

    await user.click(screen.getByRole('button', { name: /Abrir a Athena/ }))
    expect(await screen.findByRole('dialog', { name: 'Athena' })).toBeInTheDocument()
    expect(screen.getByText(/Olá, Ana\./)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Resumir publicação' }))
    expect(await screen.findByText(/não estou conectada/)).toBeInTheDocument()
  })

  it('"Prazos de hoje" responde com a agenda', async () => {
    loginAs('Lawyer')
    const { user } = renderWithProviders(<AthenaLayer />)
    await user.click(screen.getByRole('button', { name: /Abrir a Athena/ }))

    const chip = await screen.findByRole('button', { name: 'Prazos de hoje' })
    await waitFor(() => expect(chip).toBeEnabled())
    await user.click(chip)

    await waitFor(() =>
      expect(
        screen.getByText(/prazos? vencem? hoje|Não há prazos marcados para hoje/)
      ).toBeInTheDocument()
    )
  })
})
