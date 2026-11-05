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

/** Código TOTP aceito pelo mock. E-mails com "2fa" (ex.: ana.2fa@x.com) exigem o segundo fator. */
export const MOCK_MFA_CODE = '123456'
const MFA_FLAG = 'adv-mock-mfa'
const MFA_FACTOR_ID = 'mock-factor-1'

function mfaEnrolled() {
  return typeof localStorage !== 'undefined' && localStorage.getItem(MFA_FLAG) === 'enrolled'
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
  async resendConfirmation() {
    await delay(300)
  },
  async getMfaChallenge() {
    const session = readCookie()
    const required = Boolean(session?.user.email.includes('2fa')) || mfaEnrolled()
    return required && sessionStorage.getItem('adv-mock-aal2') !== session?.user.email
      ? { factorId: MFA_FACTOR_ID }
      : null
  },
  async verifyMfa(_factorId: string, code: string) {
    await delay(250)
    if (code !== MOCK_MFA_CODE) throw new AuthError('Código inválido ou expirado.', 'MFA_INVALID_CODE')
    sessionStorage.setItem('adv-mock-aal2', readCookie()?.user.email ?? '')
  },
  async hasMfaFactor() {
    return mfaEnrolled()
  },
  async enrollMfa() {
    await delay(250)
    return {
      factorId: MFA_FACTOR_ID,
      secret: 'MOCKSECRETBASE32',
      qrCode: 'data:image/svg+xml;utf8,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120"><rect width="120" height="120" fill="white" stroke="black"/></svg>'),
    }
  },
  async confirmMfaEnrollment(_factorId: string, code: string) {
    await delay(250)
    if (code !== MOCK_MFA_CODE) throw new AuthError('Código inválido.', 'MFA_INVALID_CODE')
    localStorage.setItem(MFA_FLAG, 'enrolled')
    sessionStorage.setItem('adv-mock-aal2', readCookie()?.user.email ?? '')
  },
  async updatePassword() {
    await delay(300)
  },
}
