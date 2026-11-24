import { render, screen } from '@testing-library/react'
import { Logo } from './Logo'

describe('Logo', () => {
  it('exibe o nome do produto vindo de NEXT_PUBLIC_APP_NAME (marca desacoplada)', () => {
    render(<Logo />)
    expect(screen.getByText('Iustitia')).toBeInTheDocument()
  })

  it('sem wordmark, vira uma imagem acessível com o nome do produto e só a inicial visível', () => {
    const { container } = render(<Logo showWordmark={false} />)
    expect(screen.getByRole('img', { name: 'Iustitia' })).toBeInTheDocument()
    expect(screen.queryByText('Iustitia')).not.toBeInTheDocument()
    expect(container.textContent).toBe('I.')
  })

  it('o ponto final é decorativo (não é lido pelo leitor de tela)', () => {
    const { container } = render(<Logo />)
    expect(container.querySelector('b')).toHaveAttribute('aria-hidden', 'true')
  })

  it('não desenha símbolo: a marca é só a palavra', () => {
    const { container } = render(<Logo />)
    expect(container.querySelector('svg')).toBeNull()
  })
})
