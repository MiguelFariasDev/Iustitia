import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import * as api from '../api'
import type { ProcessoListItem, ProcessosFilters } from '../types'
import { ProcessosKanban } from './ProcessosKanban'

vi.mock('../api')

const item = (id: string, status: ProcessoListItem['status'], cnj: string): ProcessoListItem => ({
  id,
  cnjNumber: cnj,
  clienteId: 'c1',
  area: 'Civel',
  status,
  tribunal: 'TJCE',
  parteContraria: 'Fulano de Tal',
  dataDistribuicao: null,
})

const dados: Record<string, ProcessoListItem[]> = {
  Ativo: [
    item('1', 'Ativo', '0000001-00.2026.8.06.0001'),
    item('2', 'Ativo', '0000002-00.2026.8.06.0001'),
  ],
  Suspenso: [item('3', 'Suspenso', '0000003-00.2026.8.06.0001')],
  Encerrado: [],
}

beforeEach(() => {
  vi.mocked(api.listProcessos).mockImplementation(async (filters: ProcessosFilters = {}) => {
    const items = dados[filters.status ?? 'Ativo'] ?? []

    return { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 }
  })
  vi.mocked(api.changeProcessoStatus).mockResolvedValue(undefined)
})

afterEach(() => vi.resetAllMocks())

function coluna(nome: RegExp) {
  return screen.getByRole('region', { name: nome })
}

async function renderKanban() {
  const utils = renderWithProviders(<ProcessosKanban filters={{}} />)
  await screen.findByText('0000001-00.2026.8.06.0001')
  return utils
}

describe('ProcessosKanban', () => {
  it('distribui os processos nas colunas por situação, com contagem', async () => {
    await renderKanban()

    expect(within(coluna(/ativos/i)).getAllByRole('article')).toHaveLength(2)
    expect(within(coluna(/suspensos/i)).getAllByRole('article')).toHaveLength(1)
    expect(within(coluna(/encerrados/i)).getByText('Nenhum processo.')).toBeInTheDocument()
  })

  it('não oferece coluna nem destino "Arquivado"', async () => {
    await renderKanban()

    expect(screen.queryByRole('region', { name: /arquivados/i })).toBeNull()
  })

  it('pede cada coluna ao servidor já filtrada pela situação e com o teto de 100', async () => {
    await renderKanban()

    const chamadas = vi.mocked(api.listProcessos).mock.calls.map(([f]) => f)
    expect(chamadas.map((f) => f?.status).sort()).toEqual(['Ativo', 'Encerrado', 'Suspenso'])
    expect(chamadas.every((f) => f?.pageSize === 100)).toBe(true)
  })

  it('arrastar para outra coluna chama a mudança de situação', async () => {
    await renderKanban()
    const cartao = within(coluna(/ativos/i)).getAllByRole('article')[0]!
    const dt = (() => {
      const data: Record<string, string> = {}
      return {
        setData: (k: string, v: string) => (data[k] = v),
        getData: (k: string) => data[k] ?? '',
        effectAllowed: '',
      }
    })()

    fireEvent.dragStart(cartao, { dataTransfer: dt })
    fireEvent.dragOver(coluna(/suspensos/i), { dataTransfer: dt })
    fireEvent.drop(coluna(/suspensos/i), { dataTransfer: dt })

    await waitFor(() => expect(api.changeProcessoStatus).toHaveBeenCalledWith('1', 'Suspenso'))
  })

  it('soltar na mesma coluna não chama o servidor', async () => {
    await renderKanban()
    const cartao = within(coluna(/ativos/i)).getAllByRole('article')[0]!
    const dt = { setData: vi.fn(), getData: () => '1', effectAllowed: '' }

    fireEvent.dragStart(cartao, { dataTransfer: dt })
    fireEvent.drop(coluna(/ativos/i), { dataTransfer: dt })

    expect(api.changeProcessoStatus).not.toHaveBeenCalled()
  })

  it('o seletor "Mover para" é o caminho por teclado', async () => {
    const { user } = await renderKanban()

    await user.click(screen.getByRole('combobox', { name: /Mover 0000001-00.2026.8.06.0001 para/ }))
    await user.click(await screen.findByRole('option', { name: 'Encerrado' }))

    await waitFor(() => expect(api.changeProcessoStatus).toHaveBeenCalledWith('1', 'Encerrado'))
  })

  it('devolve o cartão à coluna de origem quando o servidor recusa', async () => {
    vi.mocked(api.changeProcessoStatus).mockRejectedValue(new Error('transição inválida'))
    const { user } = await renderKanban()

    await user.click(screen.getByRole('combobox', { name: /Mover 0000001-00.2026.8.06.0001 para/ }))
    await user.click(await screen.findByRole('option', { name: 'Encerrado' }))

    await waitFor(() => expect(api.changeProcessoStatus).toHaveBeenCalled())
    await waitFor(() => expect(within(coluna(/ativos/i)).getAllByRole('article')).toHaveLength(2))
    expect(within(coluna(/encerrados/i)).queryAllByRole('article')).toHaveLength(0)
  })
})
