import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import type { UserRole } from '@/lib/auth/types'
import { daysFromNow } from '@/mocks/dates'

export interface InviteDetails {
  token: string
  email: string
  tenantName: string
  role: UserRole
  invitedBy: string
  expiresAt: string
}

/** Tokens especiais para exercitar estados de erro nos mocks/E2E. */
export const MOCK_EXPIRED_INVITE = 'expirado'

export async function getInvite(token: string): Promise<InviteDetails> {
  if (env.useMocks) {
    await mockResponse(null, 300)
    if (token === MOCK_EXPIRED_INVITE) {
      throw new ApiError(410, {
        errorCode: 'INVITE_EXPIRED',
        detail: 'Este convite expirou. Peça ao administrador do escritório um novo convite.',
      })
    }
    return {
      token,
      email: 'bruno.lima@escritorio.com.br',
      tenantName: 'Souza & Associados Advocacia',
      role: 'Lawyer',
      invitedBy: 'Ana Souza',
      expiresAt: daysFromNow(6),
    }
  }
  const { data } = await apiClient.get<InviteDetails>(endpoints.users.inviteByToken(token))
  return data
}

export async function acceptInvite(input: { token: string; name: string; password: string }) {
  if (env.useMocks) return mockResponse({ ok: true }, 500)
  await apiClient.post(endpoints.users.acceptInvite, input)
  return { ok: true }
}

export interface CreateTenantInput {
  officeName: string
  /** Somente dígitos. */
  cnpj: string
  adminName: string
  adminEmail: string
  adminPassword: string
}

export interface CreateTenantResult {
  tenantId: string
  adminUserId: string
  /** O Supabase exige confirmação de e-mail antes do primeiro acesso. */
  requiresEmailConfirmation: boolean
}

/**
 * Cadastro self-service de escritório (POST /tenants, anônimo). O BACKEND cria a conta no
 * Supabase Auth e o usuário titular — o front NÃO chama `signUp` antes (o wizard anterior fazia e
 * enviava nomes de campo que a API não conhece; só funcionava contra os mocks).
 */
export async function createTenant(input: CreateTenantInput): Promise<CreateTenantResult> {
  if (env.useMocks) {
    return mockResponse(
      { tenantId: crypto.randomUUID(), adminUserId: crypto.randomUUID(), requiresEmailConfirmation: true },
      500
    )
  }
  const { data } = await apiClient.post<CreateTenantResult>(endpoints.tenants.create, {
    name: input.officeName,
    cnpj: input.cnpj,
    adminName: input.adminName,
    adminEmail: input.adminEmail,
    adminPassword: input.adminPassword,
  })
  return data
}
