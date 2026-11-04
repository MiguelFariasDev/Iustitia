import { screen } from '@testing-library/react'
import { useUiStore } from '@/stores/uiStore'
import { renderWithProviders } from '@/test/render'
import { OnboardingTooltip } from './OnboardingTooltip'

const hint = (
  <OnboardingTooltip
    id="publications-intake"
    title="Recebimento de publicações"
    description="Buscamos diariamente nos diários oficiais."
  />
)

describe('OnboardingTooltip', () => {
  beforeEach(() => {
    useUiStore.setState({ hydrated: true, dismissedHints: [] })
  })

  it('não renderiza antes de o estado persistido ser reidratado', () => {
    useUiStore.setState({ hydrated: false })
    renderWithProviders(hint)
    expect(screen.queryByText('Recebimento de publicações')).not.toBeInTheDocument()
  })

  it('mostra título e descrição no primeiro acesso', () => {
    renderWithProviders(hint)
    expect(screen.getByRole('heading', { name: 'Recebimento de publicações' })).toBeInTheDocument()
    expect(screen.getByText(/diários oficiais/)).toBeInTheDocument()
  })

  it('dispensa ao confirmar e registra a dispensa', async () => {
    const { user } = renderWithProviders(hint)
    await user.click(screen.getByRole('button', { name: 'Entendi' }))
    expect(screen.queryByText('Recebimento de publicações')).not.toBeInTheDocument()
    expect(useUiStore.getState().dismissedHints).toEqual(['publications-intake'])
  })

  it('dispensa pelo botão de fechar', async () => {
    const { user } = renderWithProviders(hint)
    await user.click(screen.getByRole('button', { name: 'Fechar aviso' }))
    expect(useUiStore.getState().dismissedHints).toContain('publications-intake')
  })

  it('permanece oculto quando já foi dispensado', () => {
    useUiStore.setState({ dismissedHints: ['publications-intake'] })
    renderWithProviders(hint)
    expect(screen.queryByText('Recebimento de publicações')).not.toBeInTheDocument()
  })

  it('a dispensa de outro aviso não oculta este', () => {
    useUiStore.setState({ dismissedHints: ['agenda-week'] })
    renderWithProviders(hint)
    expect(screen.getByText('Recebimento de publicações')).toBeInTheDocument()
  })
})
