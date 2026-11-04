import { render, screen } from '@testing-library/react'
import { MiniBarChart } from './MiniBarChart'

describe('MiniBarChart', () => {
  it('expõe a série como imagem rotulada para leitores de tela', () => {
    render(<MiniBarChart data={[1, 2, 3]} label="Publicações por dia" />)
    expect(screen.getByRole('img', { name: 'Publicações por dia' })).toBeInTheDocument()
  })

  it('escala as barras pelo maior valor da série', () => {
    const { container } = render(<MiniBarChart data={[5, 10]} label="Série" />)
    const bars = container.querySelectorAll('span')
    expect(bars[0]).toHaveStyle({ height: '50%' })
    expect(bars[1]).toHaveStyle({ height: '100%' })
  })

  it('não quebra com série toda zerada', () => {
    const { container } = render(<MiniBarChart data={[0, 0]} label="Série" />)
    // Sem divisão por zero: altura mínima aplicada.
    expect(container.querySelectorAll('span')[0]).toHaveStyle({ height: '6%' })
  })

  it('mostra no máximo 12 barras, mantendo as mais recentes', () => {
    const { container } = render(
      <MiniBarChart data={Array.from({ length: 30 }, (_, i) => i + 1)} label="Série" />
    )
    expect(container.querySelectorAll('span')).toHaveLength(12)
  })

  it('não renderiza nada sem dados', () => {
    const { container } = render(<MiniBarChart data={[]} label="Série" />)
    expect(container).toBeEmptyDOMElement()
  })
})
