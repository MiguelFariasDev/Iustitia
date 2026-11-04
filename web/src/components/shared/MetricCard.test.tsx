import { render, screen } from '@testing-library/react'
import { Briefcase } from 'lucide-react'
import { MetricCard } from './MetricCard'

describe('MetricCard', () => {
  it('formata o valor em pt-BR e descreve a variação para leitores de tela', () => {
    render(<MetricCard label="Processos ativos" value={1280} delta={4} icon={Briefcase} />)
    expect(screen.getByText('1.280')).toBeInTheDocument()
    expect(screen.getByText(/aumento de/i)).toHaveClass('sr-only')
    expect(screen.getByText('+4', { exact: false })).toBeInTheDocument()
  })

  it('aumento em métrica "ruim" (prazos) usa tom de alerta', () => {
    render(
      <MetricCard label="Prazos" value={9} delta={2} icon={Briefcase} increaseIsGood={false} />
    )
    const delta = screen.getByText(/aumento de/i).parentElement
    expect(delta).toHaveClass('text-destructive-foreground')
  })

  it('sem variação, não mostra indicador', () => {
    render(<MetricCard label="Prazos" value={9} delta={0} icon={Briefcase} />)
    expect(screen.queryByText(/aumento|redução/i)).not.toBeInTheDocument()
  })

  it('renderiza o mini gráfico de tendência quando há série', () => {
    render(
      <MetricCard
        label="Não tratadas"
        value={515}
        icon={Briefcase}
        trend={[1, 2, 3]}
        trendLabel="Não tratadas por dia"
      />
    )
    expect(screen.getByRole('img', { name: 'Não tratadas por dia' })).toBeInTheDocument()
  })

  it('sem série, não renderiza gráfico', () => {
    render(<MetricCard label="Não tratadas" value={515} icon={Briefcase} />)
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })
})
