import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import { registerMockAccount } from '@/lib/auth/mockAuth'
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
  owner: OnboardingOwnerValues
  preferences: OnboardingPreferencesValues
}

/** CreateTenantResponse (Platform.Application.Features.Tenants.CreateTenant). */
export interface CreateTenantResult {
  tenantId: string
  adminUserId: string
  /**
   * Projeto Supabase com confirmação de e-mail: a conta existe, mas o titular só consegue
   * entrar depois de confirmar. A interface mostra "confirme seu e-mail" em vez de tentar
   * entrar, o que seria recusado.
   */
  requiresEmailConfirmation: boolean
}

const digits = (value: string) => value.replace(/\D/g, '')

/**
 * Cadastra a conta (tenant) E o titular, num único passo.
 *
 * É o BACKEND quem cria o usuário no Supabase Auth (CreateTenantHandler → SignUpAsync) e o
 * registro local do Owner, e quem desfaz um se o outro falhar. Por isso o frontend NÃO chama
 * `signUp` do Supabase antes: o backend tentaria cadastrar o mesmo e-mail de novo e recusaria
 * ("já existe uma conta"). Depois deste POST, quem tem sessão a obtém entrando com as
 * credenciais que acabou de definir (ver OnboardingWizard).
 *
 * Tamanho da equipe e áreas de atuação (etapa "Preferências") NÃO são enviados: o backend ainda
 * não tem onde guardá-los. Ver ADR-068, pendências.
 */
export async function createTenant(input: OnboardingInput): Promise<CreateTenantResult> {
  const { office, owner } = input

  if (env.useMocks) {
    registerMockAccount({
      email: owner.email,
      password: owner.password,
      name: owner.ownerName,
      accountType: office.accountType,
    })

    return mockResponse(
      {
        tenantId: crypto.randomUUID(),
        adminUserId: crypto.randomUUID(),
        requiresEmailConfirmation: false,
      },
      500
    )
  }

  const isSolo = office.accountType === 'Solo'
  const { data } = await apiClient.post<CreateTenantResult>(endpoints.tenants.create, {
    accountType: office.accountType,
    // Conta solo leva o nome do titular; o escritório, o nome informado.
    name: isSolo ? owner.ownerName : office.officeName,
    // O documento da conta é CPF (solo) ou CNPJ (escritório) — nunca os dois.
    cnpj: isSolo ? null : digits(office.cnpj) || null,
    cpf: isSolo ? digits(office.cpf) : null,
    adminName: owner.ownerName,
    adminEmail: owner.email,
    adminPassword: owner.password,
    adminOabNumber: digits(owner.oabNumber) || null,
    adminOabState: owner.oabNumber ? office.oabState : null,
    adminPhone: digits(office.phone) || null,
  })

  return data
}
