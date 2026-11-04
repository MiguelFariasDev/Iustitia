import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { KpiCards } from './KpiCards'

const kpis = { processosAtivos: 48, prazosVencendo: 7, tarefasConcluidas: 93 }
const areas = [
  { rotulo: 'Cível', total: 31 },
  { rotulo: 'Trabalhista', total: 24 },
  { rotulo: 'Família', total: 12 },
]

describe('KpiCards', () => {
  it('mostra os quatro indicadores com seus valores', () => {
    renderWithProviders(<KpiCards kpis={kpis} processosPorArea={areas} />)

    expect(screen.getByText('Total de processos ativos')).toBeInTheDocument()
    expect(screen.getByText('48')).toBeInTheDocument()
    expect(screen.getByText('Prazos vencendo')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
    expect(screen.getByText('nos próximos 7 dias')).toBeInTheDocument()
    expect(screen.getByText('Tarefas concluídas')).toBeInTheDocument()
    expect(screen.getByText('93')).toBeInTheDocument()
    expect(screen.getByText('Processos por área')).toBeInTheDocument()
  })

  it('o cartão de processos ativos leva à lista já filtrada', () => {
    renderWithProviders(<KpiCards kpis={kpis} processosPorArea={areas} />)

    expect(screen.getByRole('link', { name: /Processos ativos — ver a lista/ })).toHaveAttribute(
      'href',
      '/processos?status=Ativo'
    )
  })

  it('destaca a maior área e descreve a pizza por texto', () => {
    renderWithProviders(<KpiCards kpis={kpis} processosPorArea={areas} />)

    expect(screen.getByText('31 processos — a maior área')).toBeInTheDocument()
    expect(screen.getByRole('img')).toHaveAccessibleName(
      'Processos por área: Cível 31, Trabalhista 24, Família 12.'
    )
  })

  it('sem processos, não quebra nem inventa uma área', () => {
    renderWithProviders(<KpiCards kpis={kpis} processosPorArea={[]} />)

    expect(screen.getByText('Sem processos')).toBeInTheDocument()
    expect(screen.getByRole('img')).toHaveAccessibleName(
      'Processos por área: sem dados no período.'
    )
  })
})
