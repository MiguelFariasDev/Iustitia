import { render, screen } from '@testing-library/react'
import { Inbox } from 'lucide-react'
import { EmptyState } from './EmptyState'

describe('EmptyState', () => {
  it('renderiza título, descrição, ícone decorativo e ação', () => {
    const { container } = render(
      <EmptyState
        icon={Inbox}
        title="Nada aqui"
        description="Tente outro filtro"
        action={<button type="button">Limpar</button>}
      />
    )
    expect(screen.getByRole('heading', { name: 'Nada aqui' })).toBeInTheDocument()
    expect(screen.getByText('Tente outro filtro')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Limpar' })).toBeInTheDocument()
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
  })
})
