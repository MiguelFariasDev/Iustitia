import type { AuthEvent, AuthService, AuthSession, AuthUser } from './types'
import { AuthError } from './types'

/**
 * Autenticação simulada para desenvolvimento sem Supabase e testes E2E.
 * Ativada somente com NEXT_PUBLIC_AUTH_MODE=mock. A "sessão" é um cookie legível pelo
 * proxy (src/proxy.ts) — não oferece nenhuma segurança e nunca deve ir para produção.
 */
export const MOCK_SESSION_COOKIE = 'adv-mock-session'
export const MOCK_PASSWORD = 'demo1234'

export const mockUser: AuthUser = {
  id: '00000000-0000-4000-8000-000000000001',
  email: 'ana.souza@escritorio.com.br',
  name: 'Ana Souza',
  avatarUrl: null,
  role: 'Owner',
  tenantId: '00000000-0000-4000-8000-0000000000aa',
}

type Listener = (event: AuthEvent, session: AuthSession | null) => void
const listeners = new Set<Listener>()

function emit(event: AuthEvent, session: AuthSession | null) {
  listeners.forEach((listener) => listener(event, session))
}

function readCookie(): AuthSession | null {
  if (typeof document === 'undefined') return null
  const raw = document.cookie
    .split('; ')
    .find((c) => c.startsWith(`${MOCK_SESSION_COOKIE}=`))
    ?.slice(MOCK_SESSION_COOKIE.length + 1)
  if (!raw) return null
  try {
    return JSON.parse(decodeURIComponent(raw)) as AuthSession
  } catch {
    return null
  }
}

function writeCookie(session: AuthSession | null) {
  const value = session ? encodeURIComponent(JSON.stringify(session)) : ''
  const maxAge = session ? 60 * 60 * 8 : 0
  document.cookie = `${MOCK_SESSION_COOKIE}=${value}; Path=/; Max-Age=${maxAge}; SameSite=Lax`
}

function createSession(user: AuthUser): AuthSession {
  return {
    accessToken: `mock.${btoa(user.id)}.token`,
    expiresAt: Math.floor(Date.now() / 1000) + 60 * 60 * 8,
    user,
  }
}

const delay = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

export const mockAuthService: AuthService = {
  async signIn({ email, password }) {
    await delay(300)
    if (password !== MOCK_PASSWORD) {
      throw new AuthError('E-mail ou senha inválidos.', 'INVALID_CREDENTIALS')
    }
    const session = createSession({ ...mockUser, email })
    writeCookie(session)
    emit('SIGNED_IN', session)
    return session
  },
  async signUp({ email, name }) {
    await delay(300)
    const session = createSession({ ...mockUser, email, name })
    writeCookie(session)
    emit('SIGNED_IN', session)
    return session
  },
  async signOut() {
    writeCookie(null)
    emit('SIGNED_OUT', null)
  },
  async getSession() {
    return readCookie()
  },
  async refreshSession() {
    return readCookie()
  },
  onAuthStateChange(callback) {
    listeners.add(callback)
    return () => {
      listeners.delete(callback)
    }
  },
  async resetPassword() {
    await delay(300)
  },
  async updatePassword() {
    await delay(300)
  },
}
