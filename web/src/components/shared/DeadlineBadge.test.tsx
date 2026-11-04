import { render, screen } from '@testing-library/react'
import { DeadlineBadge } from './DeadlineBadge'
import { PriorityBadge } from './PriorityBadge'
import { StatusBadge } from './StatusBadge'

const now = new Date(2026, 8, 23, 10)

describe('badges', () => {
  it('DeadlineBadge indica urgência por texto (não só por cor)', () => {
    render(<DeadlineBadge dueDate={new Date(2026, 8, 22)} now={now} />)
    const badge = screen.getByText('Venceu ontem')
    expect(badge).toHaveAttribute('data-urgency', 'overdue')
  })

  it('PriorityBadge expõe "Prioridade" para leitores de tela', () => {
    render(<PriorityBadge priority="urgent" />)
    expect(screen.getByText(/prioridade/i)).toHaveClass('sr-only')
    expect(screen.getByText('Urgente')).toBeInTheDocument()
  })

  it('StatusBadge renderiza o rótulo', () => {
    render(<StatusBadge tone="success">Ativo</StatusBadge>)
    expect(screen.getByText('Ativo')).toBeInTheDocument()
  })
})
