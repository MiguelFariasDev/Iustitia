import { screen } from '@testing-library/react'
import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { useTenantStore } from '@/stores/tenantStore'
import { renderWithProviders } from '@/test/render'
import { Sidebar } from './Sidebar'

vi.mock('next/navigation', () => ({
  usePathname: () => '/painel',
  useRouter: () => ({ replace: vi.fn(), refresh: vi.fn(), push: vi.fn() }),
}))
vi.mock('@/hooks/useNavCounts', () => ({ useNavCounts: () => ({ publications: 0, alerts: 0 }) }))

afterEach(() => {
  useAuthStore.setState({ user: null })
  useTenantStore.getState().clear()
})

describe('Sidebar (v4)', () => {
  it('mostra o nome do escritório sob a marca e o usuário com o perfil no rodapé', () => {
    useAuthStore.setState({ user: { ...mockUser, role: 'Partner' } })
    useTenantStore.setState({
      currentTenant: {
        id: 't1',
        name: 'Alves & Reis Advogados',
        slug: 'alves-reis',
        plan: 'trial',
      },
    })

    renderWithProviders(<Sidebar />)

    expect(screen.getByText('Alves & Reis Advogados')).toBeInTheDocument()
    expect(screen.getByText('Ana Souza')).toBeInTheDocument()
    expect(screen.getByText('Sócio(a)')).toBeInTheDocument()
  })

  it('segue a ordem do protótipo e agrupa Indicadores em "Equipe e gestão"', () => {
    useAuthStore.setState({ user: { ...mockUser, role: 'Owner' } })

    renderWithProviders(<Sidebar />)

    const links = screen.getAllByRole('link').map((link) => link.textContent ?? '')
    const posicao = (rotulo: string) => links.findIndex((texto) => texto.startsWith(rotulo))
    expect(posicao('Agenda')).toBeLessThan(posicao('Contatos'))
    expect(posicao('Contatos')).toBeLessThan(posicao('Processos e casos'))
    expect(posicao('Processos e casos')).toBeLessThan(posicao('Alertas'))
    expect(posicao('Alertas')).toBeLessThan(posicao('Indicadores'))
    expect(screen.getByText('Equipe e gestão')).toBeInTheDocument()
  })

  it('o grupo "Equipe e gestão" aparece enquanto houver Tarefas ou Indicadores a mostrar', () => {
    useAuthStore.setState({ user: { ...mockUser, role: 'Lawyer' } })

    renderWithProviders(<Sidebar />)

    // O advogado não vê Indicadores, mas vê Tarefas: o grupo fica.
    expect(screen.getByText('Equipe e gestão')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Tarefas/ })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /Indicadores/ })).toBeNull()
  })

  it('sem Tarefas nem Indicadores (financeiro), o cabeçalho do grupo some junto', () => {
    useAuthStore.setState({ user: { ...mockUser, role: 'Financial' } })

    renderWithProviders(<Sidebar />)

    expect(screen.queryByText('Equipe e gestão')).toBeNull()
  })
})
