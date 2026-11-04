import { create } from 'zustand'
import type { AuthSession, AuthUser } from '@/lib/auth/types'

interface AuthState {
  user: AuthUser | null
  session: AuthSession | null
  isAuthenticated: boolean
  /** true até a primeira leitura da sessão (evita "piscar" a UI de visitante). */
  isLoading: boolean
  setSession: (session: AuthSession | null) => void
  setLoading: (isLoading: boolean) => void
  clear: () => void
}

export const useAuthStore = create<AuthState>()((set) => ({
  user: null,
  session: null,
  isAuthenticated: false,
  isLoading: true,
  setSession: (session) =>
    set({
      session,
      user: session?.user ?? null,
      isAuthenticated: session !== null,
      isLoading: false,
    }),
  setLoading: (isLoading) => set({ isLoading }),
  clear: () => set({ session: null, user: null, isAuthenticated: false, isLoading: false }),
}))
