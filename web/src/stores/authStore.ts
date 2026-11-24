import { create } from 'zustand'
import type { AuthSession, AuthUser } from '@/lib/auth/types'

interface AuthState {
  user: AuthUser | null
  session: AuthSession | null
  isAuthenticated: boolean
  /** true até a primeira leitura da sessão (evita "piscar" a UI de visitante). */
  isLoading: boolean
  /** Nome do cadastro no servidor (o Supabase só guarda o e-mail). Sobrevive à renovação do token. */
  profileName: { userId: string; name: string } | null
  setProfileName: (userId: string, name: string) => void
  setSession: (session: AuthSession | null) => void
  setLoading: (isLoading: boolean) => void
  clear: () => void
}

export const useAuthStore = create<AuthState>()((set, get) => ({
  user: null,
  session: null,
  isAuthenticated: false,
  isLoading: true,
  profileName: null,
  setProfileName: (userId, name) =>
    set((state) => ({
      profileName: { userId, name },
      user: state.user?.id === userId ? { ...state.user, name } : state.user,
    })),
  setSession: (session) => {
    // A renovação do token traz de volta o nome derivado do e-mail: mantém o do cadastro.
    const guardado = get().profileName
    const user =
      session && guardado?.userId === session.user.id
        ? { ...session.user, name: guardado.name }
        : (session?.user ?? null)

    set({ session, user, isAuthenticated: session !== null, isLoading: false })
  },
  setLoading: (isLoading) => set({ isLoading }),
  clear: () =>
    set({ session: null, user: null, profileName: null, isAuthenticated: false, isLoading: false }),
}))
