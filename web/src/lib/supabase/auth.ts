import type {
  AuthChangeEvent,
  Session,
  AuthError as SupabaseAuthError,
} from '@supabase/supabase-js'
import { decodeAccessTokenClaims, parseUserRole } from '@/lib/auth/claims'
import { AuthError, type AuthEvent, type AuthService, type AuthSession } from '@/lib/auth/types'
import { getSupabaseBrowserClient } from './client'

export function toAuthSession(session: Session): AuthSession {
  const claims = decodeAccessTokenClaims(session.access_token)
  const metadata = session.user.user_metadata as Record<string, unknown>
  const email = session.user.email ?? ''
  const name =
    (typeof metadata.full_name === 'string' && metadata.full_name) ||
    (typeof metadata.name === 'string' && metadata.name) ||
    email.split('@')[0] ||
    'Usuário'

  return {
    accessToken: session.access_token,
    expiresAt: session.expires_at ?? null,
    user: {
      id: session.user.id,
      email,
      name,
      avatarUrl: typeof metadata.avatar_url === 'string' ? metadata.avatar_url : null,
      role: parseUserRole(claims.user_role),
      tenantId: claims.tenant_id ?? null,
    },
  }
}

const errorMessages: Record<string, string> = {
  invalid_credentials: 'E-mail ou senha inválidos.',
  email_not_confirmed: 'Confirme seu e-mail antes de entrar.',
  user_already_exists: 'Já existe uma conta com este e-mail.',
  weak_password: 'A senha não atende aos requisitos mínimos de segurança.',
  over_request_rate_limit: 'Muitas tentativas. Aguarde alguns minutos e tente novamente.',
  over_email_send_rate_limit: 'Muitos e-mails enviados. Aguarde alguns minutos.',
  same_password: 'A nova senha deve ser diferente da atual.',
}

function toAuthError(error: SupabaseAuthError): AuthError {
  const code = error.code ?? 'AUTH_ERROR'
  return new AuthError(errorMessages[code] ?? 'Não foi possível concluir a autenticação.', code)
}

function toAuthEvent(event: AuthChangeEvent): AuthEvent | null {
  switch (event) {
    case 'SIGNED_IN':
    case 'SIGNED_OUT':
    case 'TOKEN_REFRESHED':
    case 'USER_UPDATED':
    case 'PASSWORD_RECOVERY':
      return event
    case 'INITIAL_SESSION':
      return null
    default:
      return null
  }
}

export const supabaseAuthService: AuthService = {
  async signIn({ email, password }) {
    const { data, error } = await getSupabaseBrowserClient().auth.signInWithPassword({
      email,
      password,
    })
    if (error) throw toAuthError(error)
    return toAuthSession(data.session)
  },

  async signUp({ email, password, name }) {
    const { data, error } = await getSupabaseBrowserClient().auth.signUp({
      email,
      password,
      options: {
        data: { full_name: name },
        emailRedirectTo: `${window.location.origin}/painel`,
      },
    })
    if (error) throw toAuthError(error)
    return data.session ? toAuthSession(data.session) : null
  },

  async signOut() {
    const { error } = await getSupabaseBrowserClient().auth.signOut()
    if (error) throw toAuthError(error)
  },

  async getSession() {
    const { data } = await getSupabaseBrowserClient().auth.getSession()
    return data.session ? toAuthSession(data.session) : null
  },

  async refreshSession() {
    const { data, error } = await getSupabaseBrowserClient().auth.refreshSession()
    if (error || !data.session) return null
    return toAuthSession(data.session)
  },

  onAuthStateChange(callback) {
    const { data } = getSupabaseBrowserClient().auth.onAuthStateChange((event, session) => {
      const mapped = toAuthEvent(event)
      if (mapped) callback(mapped, session ? toAuthSession(session) : null)
    })
    return () => data.subscription.unsubscribe()
  },

  async resetPassword(email) {
    const { error } = await getSupabaseBrowserClient().auth.resetPasswordForEmail(email, {
      redirectTo: `${window.location.origin}/redefinir-senha`,
    })
    if (error) throw toAuthError(error)
  },

  async updatePassword(password) {
    const { error } = await getSupabaseBrowserClient().auth.updateUser({ password })
    if (error) throw toAuthError(error)
  },
}
