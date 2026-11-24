import type { AccountType, AuthEvent, AuthService, AuthSession, AuthUser } from './types'
import { AuthError } from './types'

/**
 * Autenticação simulada para desenvolvimento sem Supabase e testes E2E.
 * Ativada somente com NEXT_PUBLIC_AUTH_MODE=mock. A "sessão" é um cookie legível pelo
 * proxy (src/proxy.ts) — não oferece nenhuma segurança e nunca deve ir para produção.
 */
export const MOCK_SESSION_COOKIE = 'adv-mock-session'
export const MOCK_PASSWORD = 'demo1234'
export const MOCK_MFA_CODE = '123456'
let mockMfaEnrolled = false

export const mockUser: AuthUser = {
  id: '00000000-0000-4000-8000-000000000001',
  email: 'ana.souza@escritorio.com.br',
  name: 'Ana Souza',
  avatarUrl: null,
  role: 'Owner',
  tenantId: '00000000-0000-4000-8000-0000000000aa',
  accountType: 'Firm',
}

/**
 * Contas "cadastradas" no mock durante esta sessão do navegador, para o fluxo de cadastro poder
 * entrar em seguida com a senha que a pessoa acabou de escolher — como o backend real exige
 * (o cadastro não devolve sessão). Só em memória: recarregar a página esquece as contas.
 */
interface MockAccount {
  password: string
  name: string
  accountType: AccountType
}
const registeredAccounts = new Map<string, MockAccount>()

export function registerMockAccount(input: MockAccount & { email: string }) {
  const { email, ...account } = input
  registeredAccounts.set(email.trim().toLowerCase(), account)
}

export function resetMockAccounts() {
  registeredAccounts.clear()
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
    const registered = registeredAccounts.get(email.trim().toLowerCase())
    if (registered ? password !== registered.password : password !== MOCK_PASSWORD) {
      throw new AuthError('E-mail ou senha inválidos.', 'INVALID_CREDENTIALS')
    }
    const session = createSession({
      ...mockUser,
      email,
      ...(registered ? { name: registered.name, accountType: registered.accountType } : {}),
    })
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
  async mfaStatus() {
    return { enrolled: mockMfaEnrolled, factorId: mockMfaEnrolled ? 'mock-factor' : null }
  },
  async mfaEnroll() {
    await delay(200)
    return {
      factorId: 'mock-factor',
      qrCode:
        'data:image/svg+xml;utf-8,<svg xmlns="http://www.w3.org/2000/svg" width="160" height="160"><rect width="160" height="160" fill="white"/><text x="80" y="84" text-anchor="middle" font-size="12">QR de teste</text></svg>',
      secret: 'MOCKSECRET234567',
    }
  },
  async mfaVerify(_factorId, code) {
    await delay(200)
    if (code !== MOCK_MFA_CODE) {
      throw new AuthError(
        'Código inválido ou expirado. Confira o aplicativo e tente de novo.',
        'mfa_verification_failed'
      )
    }
    mockMfaEnrolled = true
  },
}
