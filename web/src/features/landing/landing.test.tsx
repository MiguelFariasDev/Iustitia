import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { faqs, features, navLinks, roles } from './content'
import { HeroDemo } from './HeroDemo'
import { RolesTabs } from './RolesTabs'

function mockReducedMotion(reduced: boolean) {
  window.matchMedia = ((query: string) => ({
    matches: reduced && query.includes('prefers-reduced-motion'),
    media: query,
    onchange: null,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
    dispatchEvent: () => false,
  })) as typeof window.matchMedia
}

describe('HeroDemo', () => {
  afterEach(() => vi.useRealTimers())

  it('é demonstrativo: legenda de exemplo ilustrativo e dados fictícios', () => {
    mockReducedMotion(false)
    render(<HeroDemo />)
    expect(screen.getByText(/Exemplo ilustrativo/)).toBeInTheDocument()
  })

  it('com prefers-reduced-motion mostra só o estado final, estático, sem toast', async () => {
    mockReducedMotion(true)
    render(<HeroDemo />)

    expect(await screen.findByText('Contestação')).toBeInTheDocument()
    expect(screen.queryByText(/Prazo aceito/)).not.toBeInTheDocument()
  })

  it('anima em ciclo: sugestão aparece e depois o toast "Prazo aceito"', async () => {
    mockReducedMotion(false)
    vi.useFakeTimers()
    render(<HeroDemo />)
    expect(screen.queryByText('Contestação')).not.toBeInTheDocument()

    for (const ms of [1200, 1400]) await act(async () => void vi.advanceTimersByTime(ms))
    expect(screen.getByText('Contestação')).toBeInTheDocument()

    for (const ms of [1800, 1400]) await act(async () => void vi.advanceTimersByTime(ms))
    expect(screen.getByRole('status')).toHaveTextContent('Prazo aceito — adicionado à agenda')
  })

  it('pausa enquanto o mouse está sobre o cartão', async () => {
    mockReducedMotion(false)
    vi.useFakeTimers()
    render(<HeroDemo />)
    await act(async () => void screen.getByRole('figure').dispatchEvent(new MouseEvent('mouseover', { bubbles: true })))
    await act(async () => void vi.advanceTimersByTime(10_000))

    expect(screen.queryByText('Contestação')).not.toBeInTheDocument()
  })
})

describe('RolesTabs', () => {
  it('troca o conteúdo por perfil', async () => {
    mockReducedMotion(false)
    const user = userEvent.setup()
    render(<RolesTabs />)

    expect(screen.getByText(roles[0].headline)).toBeInTheDocument()
    await user.click(screen.getByRole('tab', { name: 'Financeiro' }))
    expect(screen.getByText(roles[3].headline)).toBeVisible()
  })
})

describe('conteúdo da landing — regras de honestidade', () => {
  const all = JSON.stringify({ features, faqs, roles, navLinks })

  it('não inventa depoimentos, métricas, selos ou preços', () => {
    expect(all).not.toMatch(/depoimento|\d+\s?%\s+dos|R\$\s?\d|\bselo\b|certificad[oa]|ISO\s?\d/i)
  })

  it('recursos que ainda não existem estão marcados', () => {
    const soon = features.filter((f) => f.status === 'Em breve').map((f) => f.title)
    expect(soon).toEqual(
      expect.arrayContaining(['Agenda', 'Atendimentos no WhatsApp', 'Criação de peças', 'Tarefas em Kanban'])
    )
  })

  it('respostas que dependem do produto carregam o marcador {{CONFIRMAR}}', () => {
    expect(faqs.filter((f) => f.a.includes('{{CONFIRMAR}}')).length).toBeGreaterThanOrEqual(2)
  })

  it('sempre associa IA a "você decide"', () => {
    expect(faqs[0]!.a).toMatch(/você decide/i)
  })
})
