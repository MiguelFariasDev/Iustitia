import { userRoles, type UserRole } from './types'

export interface AccessTokenClaims {
  sub?: string
  email?: string
  exp?: number
  tenant_id?: string
  user_role?: string
}

function decodeBase64Url(value: string) {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const binary = atob(padded)
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0))
  return new TextDecoder().decode(bytes)
}

/**
 * Lê as claims do JWT apenas para exibição (papel/escritório na UI). NUNCA usar para
 * autorização: a validação da assinatura é responsabilidade do backend (JWKS).
 */
export function decodeAccessTokenClaims(token: string): AccessTokenClaims {
  const payload = token.split('.')[1]
  if (!payload) return {}
  try {
    const parsed: unknown = JSON.parse(decodeBase64Url(payload))
    return typeof parsed === 'object' && parsed !== null ? (parsed as AccessTokenClaims) : {}
  } catch {
    return {}
  }
}

export function parseUserRole(value: unknown): UserRole | null {
  return typeof value === 'string' && (userRoles as readonly string[]).includes(value)
    ? (value as UserRole)
    : null
}
