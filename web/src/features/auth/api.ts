import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import type { UserRole } from '@/lib/auth/types'
import { daysFromNow } from '@/mocks/dates'
import type {
  OnboardingOfficeValues,
  OnboardingOwnerValues,
  OnboardingPreferencesValues,
} from './schemas'

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

export interface OnboardingInput {
  office: OnboardingOfficeValues
  owner: Omit<OnboardingOwnerValues, 'password' | 'confirmPassword'>
  preferences: OnboardingPreferencesValues
}

/** Cria o escritório (tenant) após o cadastro do responsável no Supabase Auth. */
export async function createTenant(input: OnboardingInput) {
  if (env.useMocks) return mockResponse({ tenantId: crypto.randomUUID() }, 500)
  const { data } = await apiClient.post<{ tenantId: string }>(endpoints.tenants.create, {
    name: input.office.officeName,
    cnpj: input.office.cnpj.replace(/\D/g, '') || null,
    oabState: input.office.oabState,
    phone: input.office.phone || null,
    ownerName: input.owner.ownerName,
    ownerEmail: input.owner.email,
    ownerOabNumber: input.owner.oabNumber || null,
    teamSize: input.preferences.teamSize,
    practiceAreas: input.preferences.practiceAreas,
  })
  return data
}
