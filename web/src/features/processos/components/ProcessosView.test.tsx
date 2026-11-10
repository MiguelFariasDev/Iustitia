import { screen, waitFor } from '@testing-library/react'
import { navigationState } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import * as queries from '../queries'
import { ProcessosView } from './ProcessosView'

const processosQuery = vi.fn()
vi.mock('../queries', async (importOriginal) => {
  const actual = await importOriginal<typeof queries>()
  return { ...actual, processosQuery: (...args: unknown[]) => processosQuery(...args) }
})

beforeEach(async () => {
  navigationState.searchParams = new URLSearchParams()
  const { queryOptions } = await import('@tanstack/react-query')
  processosQuery.mockImplementation((filters: { status?: string | null }) =>
    queryOptions({
      queryKey: ['processos-teste', filters.status ?? 'todos'],
      queryFn: () =>
        Promise.resolve({
          items: [
            {
              id: 'p1',
              cnjNumber: '0001234-56.2024.8.06.0001',
              tribunal: 'TJCE',
              area: 'Civel',
              parteContraria: null,
              dataDistribuicao: null,
              status: filters.status ?? 'Ativo',
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 1,
          totalPages: 1,
        }),
    })
  )
})

describe('ProcessosView', () => {
  it('filtra a situação por controle segmentado, com "Todos" marcado de início', async () => {
    const { user } = renderWithProviders(<ProcessosView />)
    await screen.findByText('0001234-56.2024.8.06.0001')

    expect(screen.getByRole('radio', { name: 'Todos' })).toBeChecked()
    for (const nome of ['Ativos', 'Suspensos', 'Encerrados', 'Arquivados'])
      expect(screen.getByRole('radio', { name: nome })).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'Suspensos' }))

    await waitFor(() => expect(screen.getByRole('radio', { name: 'Suspensos' })).toBeChecked())
    expect(processosQuery).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Suspenso' }))
  })

  it('no quadro a situação não se aplica: o filtro some', async () => {
    const { user } = renderWithProviders(<ProcessosView />)
    await screen.findByText('0001234-56.2024.8.06.0001')

    await user.click(screen.getByRole('radio', { name: 'Quadro' }))

    expect(screen.queryByRole('radiogroup', { name: 'Filtrar por situação' })).toBeNull()
  })
})
