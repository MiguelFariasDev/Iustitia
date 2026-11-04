import { mockUser } from '@/lib/auth/mockAuth'
import { useAuthStore } from './authStore'

const session = { accessToken: 'token', expiresAt: null, user: mockUser }

describe('authStore', () => {
  beforeEach(() =>
    useAuthStore.setState({ user: null, session: null, isAuthenticated: false, isLoading: true })
  )

  it('começa carregando e não autenticado', () => {
    const s = useAuthStore.getState()
    expect(s.isLoading).toBe(true)
    expect(s.isAuthenticated).toBe(false)
  })

  it('setSession deriva user/isAuthenticated e encerra o loading', () => {
    useAuthStore.getState().setSession(session)
    const s = useAuthStore.getState()
    expect(s.user).toEqual(mockUser)
    expect(s.isAuthenticated).toBe(true)
    expect(s.isLoading).toBe(false)
  })

  it('setSession(null) e clear limpam a sessão', () => {
    useAuthStore.getState().setSession(session)
    useAuthStore.getState().clear()
    expect(useAuthStore.getState()).toMatchObject({
      user: null,
      session: null,
      isAuthenticated: false,
      isLoading: false,
    })
  })
})
