import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
import { THEME_STORAGE_KEY, type Theme } from '@/config/theme'

/** Avisos de primeiro acesso, dispensáveis individualmente. */
export type OnboardingHintId = 'publications-intake' | 'alerts-classification' | 'agenda-week'

interface UiState {
  theme: Theme
  sidebarCollapsed: boolean
  mobileNavOpen: boolean
  commandPaletteOpen: boolean
  /** Falso até o localStorage ser lido: evita piscar avisos já dispensados. */
  hydrated: boolean
  dismissedHints: OnboardingHintId[]
  setTheme: (theme: Theme) => void
  toggleSidebar: () => void
  setMobileNavOpen: (open: boolean) => void
  setCommandPaletteOpen: (open: boolean) => void
  toggleCommandPalette: () => void
  dismissHint: (id: OnboardingHintId) => void
  /** Reexibe todos os avisos (ação de "rever tour" nas configurações). */
  resetHints: () => void
}

/**
 * Estado de interface. `theme`, `sidebarCollapsed` e `dismissedHints` persistem no
 * localStorage (chave compartilhada com o script anti-flash do root layout).
 * `skipHydration` evita divergência servidor/cliente: a reidratação acontece em um
 * efeito (AppProviders).
 */
export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'system',
      sidebarCollapsed: false,
      mobileNavOpen: false,
      commandPaletteOpen: false,
      hydrated: false,
      dismissedHints: [],
      setTheme: (theme) => set({ theme }),
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setMobileNavOpen: (mobileNavOpen) => set({ mobileNavOpen }),
      setCommandPaletteOpen: (commandPaletteOpen) => set({ commandPaletteOpen }),
      toggleCommandPalette: () => set((s) => ({ commandPaletteOpen: !s.commandPaletteOpen })),
      dismissHint: (id) =>
        set((s) =>
          s.dismissedHints.includes(id) ? s : { dismissedHints: [...s.dismissedHints, id] }
        ),
      resetHints: () => set({ dismissedHints: [] }),
    }),
    {
      name: THEME_STORAGE_KEY,
      storage: createJSONStorage(() => localStorage),
      partialize: (s) => ({
        theme: s.theme,
        sidebarCollapsed: s.sidebarCollapsed,
        dismissedHints: s.dismissedHints,
      }),
      skipHydration: true,
      version: 1,
      onRehydrateStorage: () => () => {
        useUiStore.setState({ hydrated: true })
      },
    }
  )
)
