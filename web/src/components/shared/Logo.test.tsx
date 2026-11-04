import { render, screen } from '@testing-library/react'
import { Logo } from './Logo'

describe('Logo', () => {
  it('exibe o nome do produto vindo de NEXT_PUBLIC_APP_NAME (marca desacoplada)', () => {
    render(<Logo />)
    expect(screen.getByText('Iustitia')).toBeInTheDocument()
  })

  it('sem wordmark, o monograma vira uma imagem acessível com o nome do produto', () => {
    render(<Logo showWordmark={false} />)
    expect(screen.getByRole('img', { name: 'Iustitia' })).toBeInTheDocument()
    expect(screen.queryByText('Iustitia')).not.toBeInTheDocument()
  })

  it('com wordmark, o SVG é decorativo (não duplica o nome para leitores de tela)', () => {
    const { container } = render(<Logo />)
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true')
    expect(screen.queryByRole('img')).not.toBeInTheDocument()
  })
})
