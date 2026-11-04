import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { ProcessosPorAreaChart } from './ProcessosPorAreaChart'

describe('ProcessosPorAreaChart', () => {
  it('é uma imagem com a descrição textual de todos os valores', () => {
    renderWithProviders(
      <ProcessosPorAreaChart
        dados={[
          { rotulo: 'Cível', total: 31 },
          { rotulo: 'Trabalhista', total: 24 },
        ]}
      />
    )

    expect(screen.getByRole('img')).toHaveAccessibleName(
      'Gráfico de rosca dos processos por área: Cível 31, Trabalhista 24.'
    )
  })

  it('sem dados, a descrição diz isso', () => {
    renderWithProviders(<ProcessosPorAreaChart dados={[]} />)

    expect(screen.getByRole('img')).toHaveAccessibleName(
      'Gráfico de rosca dos processos por área: sem dados no período.'
    )
  })
})
