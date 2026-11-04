import { THEME_STORAGE_KEY } from '@/config/theme'
import { useUiStore } from './uiStore'

describe('uiStore', () => {
  beforeEach(() =>
    useUiStore.setState({
      theme: 'system',
      sidebarCollapsed: false,
      commandPaletteOpen: false,
      mobileNavOpen: false,
      hydrated: false,
      dismissedHints: [],
    })
  )

  it('alterna sidebar e paleta de comandos', () => {
    const s = useUiStore.getState()
    s.toggleSidebar()
    s.toggleCommandPalette()
    expect(useUiStore.getState()).toMatchObject({
      sidebarCollapsed: true,
      commandPaletteOpen: true,
    })
    useUiStore.getState().setCommandPaletteOpen(false)
    expect(useUiStore.getState().commandPaletteOpen).toBe(false)
  })

  it('persiste apenas tema, sidebar e avisos dispensados (chave lida pelo script anti-flash)', () => {
    useUiStore.getState().setTheme('dark')
    useUiStore.getState().setCommandPaletteOpen(true)
    const stored = JSON.parse(localStorage.getItem(THEME_STORAGE_KEY) ?? '{}') as {
      state: Record<string, unknown>
    }
    expect(stored.state).toEqual({ theme: 'dark', sidebarCollapsed: false, dismissedHints: [] })
  })

  it('reidrata o tema salvo e marca o estado como hidratado', async () => {
    localStorage.setItem(
      THEME_STORAGE_KEY,
      JSON.stringify({ state: { theme: 'light', sidebarCollapsed: true }, version: 1 })
    )
    await useUiStore.persist.rehydrate()
    expect(useUiStore.getState()).toMatchObject({
      theme: 'light',
      sidebarCollapsed: true,
      hydrated: true,
    })
  })

  describe('avisos de onboarding', () => {
    it('registra a dispensa de um aviso', () => {
      useUiStore.getState().dismissHint('publications-intake')
      expect(useUiStore.getState().dismissedHints).toEqual(['publications-intake'])
    })

    it('não duplica a mesma dispensa', () => {
      const { dismissHint } = useUiStore.getState()
      dismissHint('agenda-week')
      dismissHint('agenda-week')
      expect(useUiStore.getState().dismissedHints).toEqual(['agenda-week'])
    })

    it('acumula dispensas de avisos diferentes', () => {
      const { dismissHint } = useUiStore.getState()
      dismissHint('publications-intake')
      dismissHint('alerts-classification')
      expect(useUiStore.getState().dismissedHints).toEqual([
        'publications-intake',
        'alerts-classification',
      ])
    })

    it('reexibe todos os avisos ao reiniciar o tour', () => {
      useUiStore.getState().dismissHint('publications-intake')
      useUiStore.getState().resetHints()
      expect(useUiStore.getState().dismissedHints).toEqual([])
    })

    it('reidrata avisos já dispensados', async () => {
      localStorage.setItem(
        THEME_STORAGE_KEY,
        JSON.stringify({ state: { dismissedHints: ['agenda-week'] }, version: 1 })
      )
      await useUiStore.persist.rehydrate()
      expect(useUiStore.getState().dismissedHints).toEqual(['agenda-week'])
    })
  })
})
