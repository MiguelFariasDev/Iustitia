import { create } from 'zustand'
import { clearSignupDraft, readSignupDraft } from '@/features/auth/signupDraft'
import type { AuthUser } from '@/lib/auth/types'
import { loadOnboarding, saveOnboarding } from './api'
import { emptyState, flowForRole, type OnboardingState } from './model'

interface OnboardingStore {
  status: 'idle' | 'loading' | 'ready'
  state: OnboardingState | null
  /** false quando o último salvamento não chegou ao servidor (fica só no navegador). */
  synced: boolean
  user: AuthUser | null
  load: (user: AuthUser) => Promise<void>
  update: (change: (state: OnboardingState) => OnboardingState) => OnboardingState | null
  clear: () => void
}

/** Estado único compartilhado: wizard, banner, checklist do painel e gate leem o mesmo. */
export const useOnboardingStore = create<OnboardingStore>()((set, get) => ({
  status: 'idle',
  state: null,
  synced: true,
  user: null,

  async load(user) {
    if (get().status === 'loading' || (get().status === 'ready' && get().user?.id === user.id)) return
    set({ status: 'loading', user })

    let state = await loadOnboarding(user)
    if (!state) {
      state = emptyState(flowForRole(user.role))
      // Aproveita o que foi digitado no cadastro (neste navegador) para não perguntar de novo.
      const draft = readSignupDraft(user.email)
      if (draft && state.flow === 'office') {
        state = {
          ...state,
          data: {
            office: { name: draft.officeName, timezone: browserTimezone(), language: 'pt-BR' },
            signup: {
              oabNumber: draft.oabNumber,
              oabState: draft.oabState,
              teamSize: draft.teamSize,
              practiceArea: draft.practiceArea,
            },
            consent: { termsAcceptedAt: draft.termsAcceptedAt, termsVersion: draft.termsVersion },
          },
        }
        clearSignupDraft()
      }
      const { synced } = await saveOnboarding(user, state)
      set({ synced })
    }
    set({ state, status: 'ready' })
  },

  update(change) {
    const { state, user } = get()
    if (!state || !user) return null
    const next = change(state)
    set({ state: next })
    // Persistência fora do caminho de interação: a UI nunca espera a rede.
    void saveOnboarding(user, next).then(({ synced }) => set({ synced }))
    return next
  },

  clear: () => set({ status: 'idle', state: null, user: null, synced: true }),
}))

export function browserTimezone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Sao_Paulo'
  } catch {
    return 'America/Sao_Paulo'
  }
}
