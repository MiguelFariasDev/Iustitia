import { screen, waitFor, within } from '@testing-library/react'
import { mockAlertasSummary, mockPushAlerta, resetAlertasMock } from '@/features/alertas/mocks'
import { renderWithProviders } from '@/test/render'
import { NotificationsDropdown } from './NotificationsDropdown'

beforeEach(() => resetAlertasMock())

describe('NotificationsDropdown (sino)', () => {
  it('mostra a contagem de não lidos no próprio nome acessível', async () => {
    renderWithProviders(<NotificationsDropdown />)

    expect(
      await screen.findByRole('button', { name: 'Notificações (4 não lidas)' })
    ).toBeInTheDocument()
  })

  it('só busca a lista quando o popover abre, e mostra os 5 alertas mais recentes', async () => {
    const { user } = renderWithProviders(<NotificationsDropdown />)
    const bell = await screen.findByRole('button', { name: 'Notificações (4 não lidas)' })

    await user.click(bell)

    const list = await screen.findByRole('list', { name: 'Últimos alertas' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(5)
    expect(screen.getByRole('link', { name: /Ver todos/ })).toHaveAttribute('href', '/alertas')
  })

  it('cada alerta leva ao detalhe, e "marcar como lido" é um botão irmão do link', async () => {
    const { user } = renderWithProviders(<NotificationsDropdown />)
    await user.click(await screen.findByRole('button', { name: 'Notificações (4 não lidas)' }))

    const item = (await screen.findAllByRole('listitem'))[0]!
    // Botão dentro de link seria HTML inválido e quebraria o teclado.
    expect(item.querySelector('a button')).toBeNull()
    expect(within(item).getByRole('link')).toHaveAttribute('href', '/alertas/alert-001')

    await user.click(within(item).getByRole('button', { name: /Marcar como lido/ }))

    await waitFor(() => expect(mockAlertasSummary().unread).toBe(3))
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Notificações (3 não lidas)' })).toBeInTheDocument()
    )
  })

  it('"Marcar todas como lidas" zera o contador', async () => {
    const { user } = renderWithProviders(<NotificationsDropdown />)
    await user.click(await screen.findByRole('button', { name: 'Notificações (4 não lidas)' }))

    await user.click(await screen.findByRole('button', { name: 'Marcar todas como lidas' }))

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Notificações' })).toBeInTheDocument()
    )
    expect(mockAlertasSummary().unread).toBe(0)
  })

  it('um alerta novo aparece no contador depois do refetch (o que o Realtime dispara)', async () => {
    const { queryClient } = renderWithProviders(<NotificationsDropdown />)
    await screen.findByRole('button', { name: 'Notificações (4 não lidas)' })

    mockPushAlerta({
      alertId: 'alert-novo',
      type: 'Publication',
      title: 'Nova publicação: despacho',
      description: 'Processo X',
      referenceId: 'pub-777',
      createdAt: new Date().toISOString(),
    })
    await queryClient.invalidateQueries({ queryKey: ['alerts'] })

    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Notificações (5 não lidas)' })).toBeInTheDocument()
    )
  })

  it('o contador acima de 9 é abreviado', async () => {
    for (let i = 0; i < 8; i++) {
      mockPushAlerta({
        alertId: `extra-${i}`,
        type: 'Other',
        title: `Extra ${i}`,
        description: null,
        referenceId: null,
        createdAt: new Date().toISOString(),
      })
    }
    renderWithProviders(<NotificationsDropdown />)

    expect(await screen.findByText('9+')).toBeInTheDocument()
  })
})
