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
  /** Reenvia o e-mail de confirmação do cadastro (resposta neutra: nunca revela se a conta existe). */
  resendConfirmation(email: string): Promise<void>

  // ── Autenticação em dois fatores (TOTP) ──────────────────────────────────────────────────
  /** Depois do login por senha: há um fator verificado que ainda precisa ser provado nesta sessão? */
  getMfaChallenge(): Promise<MfaChallenge | null>
  /** Prova o segundo fator com o código de 6 dígitos do aplicativo autenticador. */
  verifyMfa(factorId: string, code: string): Promise<void>
  /** O usuário já tem um fator TOTP verificado? */
  hasMfaFactor(): Promise<boolean>
  /** Inicia o cadastro de um fator TOTP (segredo + QR em SVG data URI). */
  enrollMfa(): Promise<MfaEnrollment>
  /** Conclui o cadastro com o primeiro código gerado pelo aplicativo. */
  confirmMfaEnrollment(factorId: string, code: string): Promise<void>
}

export interface MfaChallenge {
  factorId: string
}

export interface MfaEnrollment {
  factorId: string
  /** Segredo em Base32 para digitação manual. */
  secret: string
  /** QR Code como data URI (image/svg+xml). */
  qrCode: string
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
