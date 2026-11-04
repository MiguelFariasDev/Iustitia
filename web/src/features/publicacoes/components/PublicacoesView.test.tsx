import { screen, waitFor, within } from '@testing-library/react'
import { ApiError } from '@/lib/api/errors'
import { navigationState, routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import * as api from '../api'
import { mockListPublications, resetPublicationsMock } from '../mocks'
import { PublicacoesView } from './PublicacoesView'

// O filtro de cliente consulta a API real de clientes (sem ramo de mock): aqui basta uma lista fixa.
vi.mock('@/features/clientes/queries', async () => {
  const { queryOptions } = await import('@tanstack/react-query')
  return {
    clientesQuery: () =>
      queryOptions({
        queryKey: ['clientes-teste'],
        queryFn: () => Promise.resolve({ items: [{ id: 'cli-1', nome: 'Maria Aparecida Silva' }] }),
      }),
  }
})

const downloadCsv = vi.fn()
vi.mock('@/lib/csv', async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  downloadCsv: (...args: unknown[]) => downloadCsv(...args),
}))

vi.mock('../api', async (importOriginal) => {
  const actual = await importOriginal<typeof api>()
  return { ...actual, getPublications: vi.fn(actual.getPublications) }
})

function renderView(query = '') {
  navigationState.searchParams = new URLSearchParams(query)
  return renderWithProviders(<PublicacoesView />)
}

async function waitForRows() {
  await screen.findAllByRole('checkbox', { name: /^Selecionar publicação do processo/ })
}

const rowCheckboxes = () =>
  screen.getAllByRole('checkbox', { name: /^Selecionar publicação do processo/ })

beforeEach(() => {
  resetPublicationsMock()
  downloadCsv.mockReset()
})

describe('PublicacoesView', () => {
  it('lista a primeira página e mostra os cartões de resumo', async () => {
    renderView()
    await waitForRows()

    expect(rowCheckboxes()).toHaveLength(20)
    expect(screen.getByText(/página/i)).toHaveTextContent('Página 1 de 2')

    const cards = await screen.findByRole('region', { name: 'Resumo das publicações' })
    expect(within(cards).getByText('Não tratadas de hoje')).toBeInTheDocument()
    expect(within(cards).getByText('Não tratadas', { exact: true })).toBeInTheDocument()
    expect(within(cards).getByText('Tratadas esta semana')).toBeInTheDocument()
  })

  it('o filtro vem da URL: ?isRead=false lista só as não lidas', async () => {
    renderView('isRead=false')
    await waitForRows()

    const expected = mockListPublications({ isRead: false, pageSize: 100 }).totalCount
    expect(rowCheckboxes()).toHaveLength(expected)
    expect(screen.getByRole('button', { name: 'Não lidas' })).toHaveAttribute(
      'aria-pressed',
      'true'
    )
  })

  it('trocar um filtro navega por replace, mantém o histórico limpo e volta à página 1', async () => {
    const { user } = renderView('page=2')
    await waitForRows()

    await user.click(screen.getByRole('button', { name: 'Tratada' }))

    expect(routerMock.replace).toHaveBeenCalledTimes(1)
    const target = String(routerMock.replace.mock.calls[0]![0])
    expect(target).toContain('status=treated')
    expect(target).not.toContain('page=')
  })

  it('mudar de página atualiza a URL', async () => {
    const { user } = renderView()
    await waitForRows()

    await user.click(screen.getByRole('button', { name: 'Próxima página' }))

    expect(String(routerMock.replace.mock.calls[0]![0])).toContain('page=2')
  })

  it('ordenar pelo cabeçalho "Divulgado em" inverte a ordem na URL', async () => {
    const { user } = renderView()
    await waitForRows()

    // Padrão é "mais recente primeiro"; o clique pede o inverso (mais antigas).
    await user.click(screen.getByRole('button', { name: /divulgado em/i }))

    expect(String(routerMock.replace.mock.calls[0]![0])).toContain('ordem=oldest')
  })

  it('sem resultado, mostra o estado vazio de filtro e permite limpar', async () => {
    const { user } = renderView('busca=zzzzzz')

    expect(await screen.findByText('Nenhuma publicação encontrada')).toBeInTheDocument()

    await user.click(screen.getAllByRole('button', { name: /Limpar filtros/ })[0]!)

    expect(routerMock.replace).toHaveBeenCalledWith('/painel', { scroll: false })
  })

  describe('ações em massa', () => {
    it('marca como tratadas as selecionadas, limpa a seleção e atualiza a lista', async () => {
      const { user } = renderView('status=untreated')
      await waitForRows()

      const [first, second] = rowCheckboxes()
      await user.click(first!)
      await user.click(second!)
      expect(screen.getByText('2 selecionada(s)')).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: 'Marcar como tratada' }))

      await waitFor(() => expect(mockListPublications({ status: 'treated' }).totalCount).toBe(2))
      await waitFor(() => expect(screen.queryByText('2 selecionada(s)')).not.toBeInTheDocument())
      expect(mockListPublications({ status: 'untreated' }).totalCount).toBe(25)
    })

    it('arquiva e marca como lidas', async () => {
      const { user } = renderView('isRead=false')
      await waitForRows()

      await user.click(rowCheckboxes()[0]!)
      await user.click(screen.getByRole('button', { name: 'Marcar como lida' }))
      await waitFor(() => expect(mockListPublications({ isRead: false }).totalCount).toBe(17))
      // A seleção só é limpa quando a ação termina; clicar antes a desmarcaria de novo.
      await waitFor(() => expect(screen.queryByText('1 selecionada(s)')).not.toBeInTheDocument())

      await user.click(rowCheckboxes()[0]!)
      await user.click(screen.getByRole('button', { name: 'Arquivar' }))
      await waitFor(() => expect(mockListPublications({ status: 'archived' }).totalCount).toBe(1))
    })

    it('uma publicação recusada não impede as demais, e o resultado é informado', async () => {
      const { user } = renderView('status=untreated')
      await waitForRows()

      await user.click(rowCheckboxes()[0]!)
      await user.click(rowCheckboxes()[1]!)
      // Outra aba já revisou a primeira: "tratada" vira transição inválida para ela.
      const firstId = mockListPublications({ status: 'untreated' }).items[0]!.id
      const { mockChangeStatus } = await import('../mocks')
      mockChangeStatus(firstId, 'Rejected')

      await user.click(screen.getByRole('button', { name: 'Marcar como tratada' }))

      await waitFor(() => expect(mockListPublications({ status: 'treated' }).totalCount).toBe(1))
    })

    it('a seleção é descartada ao trocar de filtro (nada oculto é alterado)', async () => {
      const { user } = renderView()
      await waitForRows()

      await user.click(rowCheckboxes()[0]!)
      expect(screen.getByText('1 selecionada(s)')).toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: 'Lidas' }))

      expect(screen.queryByText('1 selecionada(s)')).not.toBeInTheDocument()
    })
  })

  it('exporta o CSV do filtro atual', async () => {
    const { user } = renderView()
    await waitForRows()

    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))

    await waitFor(() => expect(downloadCsv).toHaveBeenCalledTimes(1))
    const [filename, content] = downloadCsv.mock.calls[0] as [string, string]
    expect(filename).toMatch(/^publicacoes-\d{4}-\d{2}-\d{2}\.csv$/)
    expect(content).toContain('Divulgado em;Tipo;Processo')
    // Todas as 27, não só a página visível.
    expect(content.split('\r\n')).toHaveLength(28)
  })

  it('mostra o erro da consulta com opção de tentar de novo', async () => {
    vi.mocked(api.getPublications).mockRejectedValueOnce(
      new ApiError(500, { detail: 'Falha no servidor', correlationId: 'abc-123' })
    )
    const { user } = renderView()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível carregar as publicações'
    )
    expect(screen.getByText('Código de suporte: abc-123')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }))

    await waitForRows()
  })
})
