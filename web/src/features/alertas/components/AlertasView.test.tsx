import { screen, waitFor, within } from '@testing-library/react'
import { navigationState } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { mockAlertasSummary, mockListAlertas, resetAlertasMock } from '../mocks'
import { AlertasView } from './AlertasView'
import { MarcarTodosButton } from './MarcarTodosButton'

function renderView(query = '') {
  navigationState.searchParams = new URLSearchParams(query)
  return renderWithProviders(
    <>
      <MarcarTodosButton />
      <AlertasView />
    </>
  )
}

beforeEach(() => resetAlertasMock())

describe('AlertasView', () => {
  it('abre em "Importantes" (não lidos) com a contagem de cada aba', async () => {
    renderView()

    expect(await screen.findByRole('tab', { name: 'Importantes (4)' })).toHaveAttribute(
      'aria-selected',
      'true'
    )
    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual([
      'Importantes (4)',
      'Publicações (3)',
      'Prazos (1)',
      'Todos (6)',
    ])

    const list = await screen.findByRole('list', { name: 'Importantes' })
    expect(within(list).getAllByRole('article')).toHaveLength(4)
  })

  it('cada aba filtra a mesma lista', async () => {
    const { user } = renderView()
    await screen.findByRole('list', { name: 'Importantes' })

    // A lista da aba anterior continua visível (dado anterior) até a nova chegar, então a
    // asserção espera a contagem — não a mera existência da lista.
    const articlesIn = (name: string) => () =>
      within(screen.getByRole('list', { name })).getAllByRole('article')

    await user.click(screen.getByRole('tab', { name: /^Todos/ }))
    await waitFor(() => expect(articlesIn('Todos')()).toHaveLength(6))

    await user.click(screen.getByRole('tab', { name: /^Publicações/ }))
    await waitFor(() => expect(articlesIn('Publicações')()).toHaveLength(3))

    await user.click(screen.getByRole('tab', { name: /^Prazos/ }))
    await waitFor(() => expect(articlesIn('Prazos')()).toHaveLength(1))
    expect(
      within(screen.getByRole('list', { name: 'Prazos' })).getByText('Tarefa vence amanhã')
    ).toBeInTheDocument()
  })

  it('troca de aba pelo teclado (setas)', async () => {
    const { user } = renderView()
    const tab = await screen.findByRole('tab', { name: /^Importantes/ })

    tab.focus()
    await user.keyboard('{ArrowRight}')

    expect(screen.getByRole('tab', { name: /^Publicações/ })).toHaveAttribute(
      'aria-selected',
      'true'
    )
  })

  it('a aba inicial pode vir da URL', async () => {
    renderView('aba=deadlines')

    expect(await screen.findByRole('tab', { name: /^Prazos/ })).toHaveAttribute(
      'aria-selected',
      'true'
    )
  })

  it('descartar remove o alerta e atualiza as contagens', async () => {
    const { user } = renderView()
    await screen.findByRole('list', { name: 'Importantes' })

    await user.click(screen.getByRole('button', { name: 'Descartar alerta: Tarefa vence amanhã' }))

    await waitFor(() =>
      expect(screen.getByRole('tab', { name: 'Importantes (3)' })).toBeInTheDocument()
    )
    expect(screen.getByRole('tab', { name: 'Todos (5)' })).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('Tarefa vence amanhã')).not.toBeInTheDocument())
    expect(mockListAlertas({}).totalCount).toBe(5)
  })

  it('marcar como lida tira o alerta de "Importantes" mas ele segue em "Todos"', async () => {
    const { user } = renderView()
    await screen.findByRole('list', { name: 'Importantes' })

    await user.click(
      screen.getByRole('button', { name: 'Marcar como lida: Aniversário de cliente' })
    )

    await waitFor(() =>
      expect(screen.getByRole('tab', { name: 'Importantes (3)' })).toBeInTheDocument()
    )
    expect(screen.getByRole('tab', { name: 'Todos (6)' })).toBeInTheDocument()
  })

  it('"Marcar todos como lidos" zera as não lidas e mostra o estado vazio', async () => {
    const { user } = renderView()
    await screen.findByRole('list', { name: 'Importantes' })

    await user.click(screen.getByRole('button', { name: 'Marcar todos como lidos' }))

    expect(await screen.findByText('Nenhum alerta importante no momento')).toBeInTheDocument()
    expect(mockAlertasSummary().unread).toBe(0)
    expect(screen.getByRole('button', { name: 'Marcar todos como lidos' })).toBeDisabled()
  })

  it('na aba "Todos", filtra por tipo e por leitura', async () => {
    const { user } = renderView('aba=all')
    await screen.findByRole('list', { name: 'Todos' })

    await user.click(screen.getByRole('button', { name: 'Lidos' }))
    await waitFor(() =>
      expect(
        within(screen.getByRole('list', { name: 'Todos' })).getAllByRole('article')
      ).toHaveLength(2)
    )

    await user.click(screen.getByRole('button', { name: /Limpar filtros/ }))
    await waitFor(() =>
      expect(
        within(screen.getByRole('list', { name: 'Todos' })).getAllByRole('article')
      ).toHaveLength(6)
    )
  })

  it('o filtro de tipo e leitura NÃO existe nas abas que já os definem', async () => {
    renderView()
    await screen.findByRole('list', { name: 'Importantes' })

    // "Importantes" já é "não lidos": oferecer "Lidos" permitiria uma combinação contraditória.
    expect(screen.queryByRole('button', { name: 'Lidos' })).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: 'Filtrar por tipo' })).not.toBeInTheDocument()
    expect(screen.getByLabelText('Recebidos a partir de')).toBeInTheDocument()
  })
})
