import { screen, within } from '@testing-library/react'
import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { navigationState } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { BottomNav } from './BottomNav'

function renderAs(role: string, path = '/painel') {
  navigationState.pathname = path
  useAuthStore.setState({ user: { ...mockUser, role: role as never } })
  return renderWithProviders(<BottomNav />)
}

afterEach(() => useAuthStore.setState({ user: null }))

const nomes = () =>
  within(screen.getByRole('navigation', { name: 'Navegação rápida' }))
    .getAllByRole('link')
    .map((l) => l.textContent)

describe('BottomNav (celular)', () => {
  it('advogado: Painel, Revisão e Atender, só abaixo de md', () => {
    renderAs('Lawyer')

    expect(nomes()).toEqual(['Painel', 'Revisão', 'Atender'])
    expect(screen.getByRole('navigation', { name: 'Navegação rápida' }).className).toMatch(
      /md:hidden/
    )
  })

  it('financeiro só tem o painel: as outras abas levariam a um 403', () => {
    renderAs('Financial')

    expect(nomes()).toEqual(['Painel'])
  })

  it('marca a aba da tela atual com aria-current', () => {
    renderAs('Lawyer', '/atendimentos')

    expect(screen.getByRole('link', { name: 'Atender' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Painel' })).not.toHaveAttribute('aria-current')
  })

  it('Revisão aponta para a fila da IA', () => {
    renderAs('Partner', '/publicacoes/revisao')

    expect(screen.getByRole('link', { name: 'Revisão' })).toHaveAttribute(
      'href',
      '/publicacoes/revisao'
    )
    expect(screen.getByRole('link', { name: 'Revisão' })).toHaveAttribute('aria-current', 'page')
  })
})
