import { render, screen } from '@testing-library/react'
import { PageHeader } from './PageHeader'

describe('PageHeader', () => {
  it('renderiza título como h1, descrição e ações', () => {
    render(
      <PageHeader
        title="Painel"
        description="Visão geral"
        actions={<button type="button">Exportar</button>}
      />
    )
    expect(screen.getByRole('heading', { level: 1, name: 'Painel' })).toBeInTheDocument()
    expect(screen.getByText('Visão geral')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar' })).toBeInTheDocument()
  })

  it('omite descrição e ações quando ausentes', () => {
    const { container } = render(<PageHeader title="Painel" />)
    expect(container.querySelectorAll('p')).toHaveLength(0)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
