import { screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { mockFluxoCaixa, mockListLancamentos } from '../mocks'
import { FinanceiroTabs } from './FinanceiroTabs'

const downloadCsv = vi.fn()
vi.mock('@/lib/csv', async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  downloadCsv: (...args: unknown[]) => downloadCsv(...args),
}))

const lancamentos = mockListLancamentos()
const atrasados = lancamentos.filter((l) => l.status === 'Atrasado')

function renderTabs() {
  return renderWithProviders(
    <FinanceiroTabs lancamentos={lancamentos} fluxoCaixa={mockFluxoCaixa()} />
  )
}

beforeEach(() => downloadCsv.mockReset())

describe('FinanceiroTabs', () => {
  it('abas na ordem do v4: Honorários, A receber, A pagar, Em atraso (e o fluxo de caixa)', () => {
    renderTabs()

    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual([
      'Honorários',
      'A receber',
      'A pagar',
      'Em atraso',
      'Fluxo de caixa',
    ])
  })

  it('"Em atraso" junta todos os tipos e só os vencidos, sem o filtro de status', async () => {
    const { user } = renderTabs()

    await user.click(screen.getByRole('tab', { name: 'Em atraso' }))

    expect(atrasados.length).toBeGreaterThan(0)
    const tabela = await screen.findByRole('table', { name: 'Lançamentos em atraso' })
    // cabeçalho + uma linha por lançamento em atraso
    expect(within(tabela).getAllByRole('row')).toHaveLength(atrasados.length + 1)
    expect(screen.queryByRole('radiogroup', { name: 'Filtrar por status' })).toBeNull()
  })

  it('o filtro de status é segmentado, com "Todos" marcado', () => {
    renderTabs()

    expect(screen.getByRole('radio', { name: 'Todos' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Pago' })).toBeInTheDocument()
  })

  it('exporta em CSV só o que a aba "Em atraso" mostra', async () => {
    const { user } = renderTabs()
    await user.click(screen.getByRole('tab', { name: 'Em atraso' }))

    await user.click(screen.getByRole('button', { name: /Exportar/ }))
    await user.click(await screen.findByRole('menuitem', { name: 'Exportar em CSV' }))

    expect(downloadCsv).toHaveBeenCalledTimes(1)
    const [nome, conteudo] = downloadCsv.mock.calls[0] as [string, string]
    expect(nome).toBe('financeiro-atraso.csv')
    expect(conteudo.split('\r\n')).toHaveLength(atrasados.length + 1)
  })
})
