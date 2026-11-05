import { render, screen } from '@testing-library/react'
import { Breadcrumbs } from './Breadcrumbs'

let pathname = '/painel'
vi.mock('next/navigation', () => ({ usePathname: () => pathname }))

describe('Breadcrumbs (Iustitia / tela)', () => {
  it('começa pelo nome do produto, que leva ao painel', () => {
    pathname = '/agenda'
    render(<Breadcrumbs />)

    expect(screen.getByRole('link', { name: 'Iustitia' })).toHaveAttribute('href', '/painel')
    expect(screen.getByText('Agenda')).toHaveAttribute('aria-current', 'page')
  })

  it('no painel mostra "Iustitia / Área de trabalho" sem duplicar a chave', () => {
    pathname = '/painel'
    render(<Breadcrumbs />)

    expect(screen.getAllByRole('listitem')).toHaveLength(2)
    expect(screen.getByText('Área de trabalho')).toHaveAttribute('aria-current', 'page')
  })

  it('usa o rótulo "Processos e casos" e mantém os níveis mais profundos', () => {
    pathname = '/processos/novo'
    render(<Breadcrumbs />)

    expect(screen.getByRole('link', { name: 'Processos e casos' })).toBeInTheDocument()
    expect(screen.getByText('Novo')).toHaveAttribute('aria-current', 'page')
  })
})
