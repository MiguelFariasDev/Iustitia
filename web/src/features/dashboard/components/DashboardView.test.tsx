import { screen, within } from '@testing-library/react'
import { Suspense } from 'react'
import { createTestQueryClient, renderWithProviders } from '@/test/render'
import { queryKeys } from '@/lib/query/queryKeys'
import type { DashboardSummary } from '../types'
import { DashboardView } from './DashboardView'

function summary(overrides: Partial<DashboardSummary> = {}): DashboardSummary {
  return {
    metrics: {
      activeProcesses: { value: 12, delta: 0 },
      unreadPublications: { value: 7, delta: 0 },
      pendingReviews: { value: 3, delta: 0 },
      unreadAlerts: { value: 2, delta: 0 },
    },
    publications: [
      {
        id: 'pub-1',
        source: 'TJSP',
        processNumber: '0165738-43.2026.1.00.0000',
        client: 'Maria Silva',
        excerpt: 'Fica a parte intimada…',
        publishedAt: '2026-09-27T00:00:00+00:00',
      },
    ],
    alerts: [
      {
        id: 'alert-1',
        title: 'Nova publicação',
        description: 'Processo 0165738-43.2026.1.00.0000',
        type: 'Publication',
        createdAt: '2026-09-27T10:00:00+00:00',
      },
    ],
    ...overrides,
  }
}

function render(data: DashboardSummary = summary()) {
  const queryClient = createTestQueryClient()
  queryClient.setQueryData(queryKeys.dashboard.summary(), data)
  renderWithProviders(<DashboardView />, { queryClient })
}

describe('DashboardView', () => {
  it('renderiza indicadores a partir do cache hidratado', () => {
    render()

    const metrics = screen.getByRole('region', { name: 'Indicadores' })
    expect(within(metrics).getByText('Processos ativos')).toBeInTheDocument()
    expect(within(metrics).getByText('Aguardando revisão humana')).toBeInTheDocument()
    expect(within(metrics).getByText('Alertas')).toBeInTheDocument()
  })

  it('o cartão de publicações não lidas leva à lista já filtrada', () => {
    render()

    expect(
      screen.getByRole('link', { name: 'Publicações não lidas — ver a lista' })
    ).toHaveAttribute('href', '/publicacoes?isRead=false')
    expect(screen.getByRole('link', { name: 'Alertas não lidos — ver a lista' })).toHaveAttribute(
      'href',
      '/alertas'
    )
  })

  it('cada publicação recente leva ao seu detalhe, e "Ver todas" à lista de não lidas', () => {
    render()

    const list = screen.getByRole('list', { name: 'Publicações não lidas' })
    expect(within(list).getByRole('link')).toHaveAttribute('href', '/publicacoes/pub-1')
    expect(within(list).getByText('Maria Silva')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Ver todas/ })).toHaveAttribute(
      'href',
      '/publicacoes?isRead=false'
    )
  })

  it('cada alerta recente leva ao seu detalhe, e "Ver todos" à lista de alertas', () => {
    render()

    const list = screen.getByRole('list', { name: 'Alertas' })
    expect(within(list).getByRole('link')).toHaveAttribute('href', '/alertas/alert-1')
    expect(screen.getByRole('link', { name: /Ver todos/ })).toHaveAttribute('href', '/alertas')
  })

  /**
   * Agenda e prazos não existem no backend (Fases 3 e 4). Exibi-los com dado inventado levaria
   * a advogada a confiar num prazo que ninguém registrou.
   */
  it('não exibe agenda nem prazos', () => {
    render()

    expect(screen.queryByRole('heading', { name: 'Agenda de hoje' })).not.toBeInTheDocument()
    expect(screen.queryByText('Prazos nesta semana')).not.toBeInTheDocument()
  })

  it('exibe estados vazios quando não há publicações nem alertas', () => {
    render(summary({ publications: [], alerts: [] }))

    expect(screen.getByText('Nenhuma publicação pendente')).toBeInTheDocument()
    expect(screen.getByText('Nenhum alerta novo')).toBeInTheDocument()
  })

  it('suspende (fallback) enquanto os dados carregam', async () => {
    renderWithProviders(
      <Suspense fallback={<p>carregando…</p>}>
        <DashboardView />
      </Suspense>
    )
    expect(screen.getByText('carregando…')).toBeInTheDocument()
  })
})
