/** Espelha Platform.Domain.Identity.UserRole. */
export const userRoles = ['Owner', 'Partner', 'Lawyer', 'Intern', 'Admin', 'Financial'] as const
export type UserRole = (typeof userRoles)[number]

export const userRoleLabels: Record<UserRole, string> = {
  Owner: 'Titular',
  Partner: 'Sócio(a)',
  Lawyer: 'Advogado(a)',
  Intern: 'Estagiário(a)',
  Admin: 'Administrativo',
  Financial: 'Financeiro',
}

export interface AuthUser {
  id: string
  email: string
  name: string
  avatarUrl: string | null
  /** Claim `user_role` emitida pelo Custom Access Token Hook (ADR-029). */
  role: UserRole | null
  /** Claim `tenant_id` emitida pelo Custom Access Token Hook (ADR-029). */
  tenantId: string | null
}

export interface AuthSession {
  accessToken: string
  expiresAt: number | null
  user: AuthUser
}

export type AuthEvent =
  'SIGNED_IN' | 'SIGNED_OUT' | 'TOKEN_REFRESHED' | 'USER_UPDATED' | 'PASSWORD_RECOVERY'

export interface Credentials {
  email: string
  password: string
}

export interface SignUpInput extends Credentials {
  name: string
}

export interface AuthService {
  signIn(credentials: Credentials): Promise<AuthSession>
  signUp(input: SignUpInput): Promise<AuthSession | null>
  signOut(): Promise<void>
  getSession(): Promise<AuthSession | null>
  refreshSession(): Promise<AuthSession | null>
  onAuthStateChange(callback: (event: AuthEvent, session: AuthSession | null) => void): () => void
  resetPassword(email: string): Promise<void>
  updatePassword(password: string): Promise<void>
}

export class AuthError extends Error {
  constructor(
    message: string,
    readonly code: string = 'AUTH_ERROR'
  ) {
    super(message)
    this.name = 'AuthError'
  }
}
