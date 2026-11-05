import { render, screen } from '@testing-library/react'
import type { UserRole, AccountType } from '@/lib/auth/types'
import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from '@/stores/authStore'
import { AccessGuard } from './AccessGuard'

let pathname = '/processos'
vi.mock('next/navigation', () => ({ usePathname: () => pathname }))

function loginAs(role: UserRole | null, accountType: AccountType | null) {
  useAuthStore.setState({ user: { ...mockUser, role, accountType } })
}

afterEach(() => useAuthStore.setState({ user: null }))

describe('AccessGuard', () => {
  it('mostra a tela quando o papel alcança', () => {
    pathname = '/processos'
    loginAs('Lawyer', 'Firm')
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.getByText('conteúdo')).toBeInTheDocument()
  })

  it('barra a tela que o papel não alcança', () => {
    pathname = '/financeiro'
    loginAs('Lawyer', 'Firm')
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.queryByText('conteúdo')).toBeNull()
    expect(screen.getByRole('alert')).toHaveTextContent('Você não tem acesso a esta tela')
  })

  it('conta Solo não abre a tela de usuários, nem digitando o endereço', () => {
    pathname = '/configuracoes/usuarios'
    loginAs('Owner', 'Solo')
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('o titular do escritório abre a tela de usuários', () => {
    pathname = '/configuracoes/usuarios'
    loginAs('Owner', 'Firm')
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.getByText('conteúdo')).toBeInTheDocument()
  })

  it('rota fora da matriz passa', () => {
    pathname = '/configuracoes'
    loginAs('Intern', 'Firm')
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.getByText('conteúdo')).toBeInTheDocument()
  })

  it('sem usuário carregado deixa passar (a API continua barrando)', () => {
    pathname = '/financeiro'
    render(<AccessGuard>conteúdo</AccessGuard>)

    expect(screen.getByText('conteúdo')).toBeInTheDocument()
  })
})
