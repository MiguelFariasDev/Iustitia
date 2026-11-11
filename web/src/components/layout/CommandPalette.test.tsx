import { screen, waitFor, within } from '@testing-library/react'
import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import { routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import CommandPalette from './CommandPalette'

function abrir(role: 'Owner' | 'Financial' = 'Owner') {
  useAuthStore.setState({ user: { ...mockUser, role } })
  useUiStore.setState({ commandPaletteOpen: true })
  return renderWithProviders(<CommandPalette />)
}

beforeEach(() => routerMock.push.mockClear())

afterEach(() => {
  useAuthStore.setState({ user: null })
  useUiStore.setState({ commandPaletteOpen: false })
})

describe('CommandPalette (v4)', () => {
  it('é uma janela de 560px a 14% do topo, centrada só na horizontal', async () => {
    abrir()
    const janela = await screen.findByRole('dialog', { name: 'Busca global' })

    expect(janela.className).toMatch(/sm:max-w-\[560px\]/)
    expect(janela.className).toMatch(/top-\[14vh\]/)
    expect(janela.className).toMatch(/translate-y-0/)
    expect(janela.className).toMatch(/left-\[50%\]/)
    expect(janela.className).toMatch(/translate-x-\[-50%\]/)
  })

  it('lista as telas do menu que o papel pode abrir', async () => {
    abrir('Financial')
    await screen.findByRole('dialog', { name: 'Busca global' })

    const lista = screen.getByRole('listbox')
    expect(within(lista).getByText('Ir para Agenda')).toBeInTheDocument()
    // O financeiro não acessa processos: o atalho não pode existir.
    expect(within(lista).queryByText('Ir para Processos e casos')).toBeNull()
  })

  it('Enter abre o primeiro resultado', async () => {
    const { user } = abrir()
    await screen.findByRole('dialog', { name: 'Busca global' })

    await user.keyboard('{Enter}')

    // A primeira tela do menu é a área de trabalho.
    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith('/painel'))
  })

  it('filtrar pelo texto e dar Enter abre a tela encontrada', async () => {
    const { user } = abrir()
    await screen.findByRole('dialog', { name: 'Busca global' })

    await user.type(screen.getByLabelText('Termo de busca'), 'agenda')
    await user.keyboard('{Enter}')

    await waitFor(() => expect(routerMock.push).toHaveBeenCalledWith('/agenda'))
  })
})
