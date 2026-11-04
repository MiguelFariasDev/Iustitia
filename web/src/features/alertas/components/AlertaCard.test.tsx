import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import type { Alerta } from '../types'
import { AlertaCard } from './AlertaCard'

function alerta(overrides: Partial<Alerta> = {}): Alerta {
  return {
    id: 'a1',
    type: 'Publication',
    title: 'Nova publicação',
    description: 'Processo 1234567-48.2026.8.26.0001',
    referenceId: 'pub-9',
    isRead: false,
    createdAt: new Date().toISOString(),
    readAt: null,
    ...overrides,
  }
}

describe('AlertaCard', () => {
  it('alerta de publicação oferece abrir a publicação e marcar como lido', async () => {
    const onMarkAsRead = vi.fn()
    const { user } = renderWithProviders(
      <AlertaCard alerta={alerta()} onMarkAsRead={onMarkAsRead} onDismiss={vi.fn()} />
    )

    expect(screen.getByRole('heading', { name: 'Nova publicação' })).toBeInTheDocument()
    expect(screen.getByText('Nova')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Ver publicação/ })).toHaveAttribute(
      'href',
      '/publicacoes/pub-9'
    )

    await user.click(screen.getByRole('button', { name: /Marcar como lida/ }))
    expect(onMarkAsRead).toHaveBeenCalledWith('a1')
  })

  it('abrir a publicação a partir do alerta o marca como lido', async () => {
    const onMarkAsRead = vi.fn()
    const { user } = renderWithProviders(
      <AlertaCard alerta={alerta()} onMarkAsRead={onMarkAsRead} onDismiss={vi.fn()} />
    )

    await user.click(screen.getByRole('link', { name: /Ver publicação/ }))

    expect(onMarkAsRead).toHaveBeenCalledWith('a1')
  })

  it('alerta já lido não oferece "marcar como lida" nem a marca "Nova"', () => {
    renderWithProviders(
      <AlertaCard alerta={alerta({ isRead: true })} onMarkAsRead={vi.fn()} onDismiss={vi.fn()} />
    )

    expect(screen.queryByRole('button', { name: /Marcar como lida/ })).not.toBeInTheDocument()
    expect(screen.queryByText('Nova')).not.toBeInTheDocument()
  })

  it('alerta que não é de publicação não tem link para publicação', () => {
    renderWithProviders(
      <AlertaCard
        alerta={alerta({ type: 'Birthday', referenceId: null })}
        onMarkAsRead={vi.fn()}
        onDismiss={vi.fn()}
      />
    )

    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(screen.getByText('Aniversário')).toBeInTheDocument()
  })

  it('descartar tem nome acessível com o título do alerta', async () => {
    const onDismiss = vi.fn()
    const { user } = renderWithProviders(
      <AlertaCard alerta={alerta()} onMarkAsRead={vi.fn()} onDismiss={onDismiss} />
    )

    await user.click(screen.getByRole('button', { name: 'Descartar alerta: Nova publicação' }))

    expect(onDismiss).toHaveBeenCalledWith('a1')
  })

  it('o texto do alerta é renderizado como texto, nunca como HTML', () => {
    renderWithProviders(
      <AlertaCard
        alerta={alerta({ title: '<img src=x onerror=alert(1)>' })}
        onMarkAsRead={vi.fn()}
        onDismiss={vi.fn()}
      />
    )

    expect(screen.getByRole('heading')).toHaveTextContent('<img src=x onerror=alert(1)>')
    expect(document.querySelector('img')).toBeNull()
  })
})
