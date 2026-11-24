import { render, screen, within } from '@testing-library/react'
import LandingPage from '@/app/(marketing)/page'
import MarketingLayout from '@/app/(marketing)/layout'
import { features, statusLabel } from '../content'

function renderLanding() {
  return render(
    <MarketingLayout>
      <LandingPage />
    </MarketingLayout>
  )
}

describe('landing page', () => {
  it('tem um único h1 e a navegação por seções', () => {
    renderLanding()
    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
    const nav = screen.getByRole('navigation', { name: 'Seções da página' })
    expect(within(nav).getByRole('link', { name: 'Recursos' })).toHaveAttribute('href', '/#recursos')
  })

  it('leva às telas de entrar e de cadastro', () => {
    renderLanding()
    expect(screen.getAllByRole('link', { name: /^Entrar$/ })[0]).toHaveAttribute('href', '/login')
    expect(screen.getAllByRole('link', { name: /Criar conta|Cadastre seu escritório/ })[0]).toHaveAttribute(
      'href',
      '/onboarding'
    )
  })

  it('lista todos os recursos e marca os que ainda não estão prontos', () => {
    renderLanding()
    for (const feature of features) {
      expect(screen.getByText(feature.title)).toBeInTheDocument()
    }
    expect(screen.getAllByText(statusLabel['em-evolucao']!).length).toBeGreaterThan(0)
    expect(screen.getAllByText(statusLabel.beta!).length).toBeGreaterThan(0)
  })

  it('avisa que a ilustração tem dados fictícios e que o acervo não é oficial', () => {
    renderLanding()
    expect(screen.getByText('Ilustração com dados fictícios.')).toBeInTheDocument()
    expect(screen.getAllByText(/não substituem a publicação oficial/).length).toBeGreaterThan(0)
  })

  it('não promete resultado nem usa números inventados', () => {
    const { container } = renderLanding()
    const text = container.textContent ?? ''
    expect(text).not.toMatch(/\d+\s?%/)
    expect(text).not.toMatch(/garant(e|imos|ido) (resultado|sucesso)|o melhor|número 1/i)
  })
})
