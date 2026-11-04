import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { mockResponse } from '@/lib/api/mock'
import {
  toAuditEntry,
  toTeamUser,
  type ListAuditLogsDto,
  type ListUsersDto,
  type MeDto,
  type TenantDto,
  type UserDetailDto,
} from './contract'
import {
  auditEntriesMock,
  integrationsMock,
  notificationPreferencesMock,
  profileMock,
  teamUsersMock,
  tenantProfileMock,
} from './mocks'
import type { InviteUserValues, ProfileValues, TenantProfileValues } from './schemas'
import type {
  AuditEntry,
  Integration,
  NotificationPreference,
  Profile,
  TeamUser,
  TenantProfile,
} from './types'

// Estado em memória dos mocks (simula persistência durante a sessão de desenvolvimento).
let users = [...teamUsersMock]
let profile = { ...profileMock }
let tenantProfile = { ...tenantProfileMock }
let preferences = structuredClone(notificationPreferencesMock)

export async function getProfile(): Promise<Profile> {
  if (env.useMocks) return mockResponse(profile, 250)

  // /me devolve apenas identidade e papéis (é o que existe no JWT). Nome, telefone e OAB
  // vivem na tabela users, então o perfil é composto: /me identifica, /users/{id} detalha.
  const { data: me } = await apiClient.get<MeDto>(endpoints.status.me)
  if (!me.userId) {
    throw new Error('Sessão sem usuário identificado.')
  }

  const { data: user } = await apiClient.get<UserDetailDto>(endpoints.users.byId(me.userId))

  return {
    name: user.name,
    email: user.email || me.email || '',
    phone: user.phone ?? '',
    // GetUserByIdResponse não expõe o número da OAB (o agregado User o guarda, o DTO de
    // leitura não o devolve). Os campos ficam vazios em vez de inventados — habilitá-los
    // exige acrescentar OabNumber ao contrato do backend.
    oabNumber: '',
    oabState: '',
  }
}

export async function updateProfile(values: ProfileValues): Promise<Profile> {
  if (env.useMocks) {
    profile = { ...values }
    return mockResponse(profile, 400)
  }
  // Não existe PUT /me: a edição do próprio cadastro passa por PUT /users/{id}
  // (UpdateUserCommand), que aceita nome, especialidade, avatar e telefone.
  const { data: me } = await apiClient.get<MeDto>(endpoints.status.me)
  if (!me.userId) {
    throw new Error('Sessão sem usuário identificado.')
  }

  await apiClient.put(endpoints.users.byId(me.userId), {
    name: values.name,
    specialty: null,
    avatarUrl: null,
    phone: values.phone,
  })

  return getProfile()
}

export async function getTenantProfile(): Promise<TenantProfile> {
  if (env.useMocks) return mockResponse(tenantProfile, 250)

  // GET /settings devolve pares chave/valor do escritório, não o cadastro — o cadastro é
  // o agregado Tenant, em GET /tenants/{id}.
  const { data: me } = await apiClient.get<MeDto>(endpoints.status.me)
  if (!me.tenantId) {
    throw new Error('Sessão sem escritório identificado.')
  }

  const { data: tenant } = await apiClient.get<TenantDto>(endpoints.tenants.byId(me.tenantId))

  return {
    name: tenant.name,
    cnpj: tenant.cnpj,
    // GetTenantByIdResponse ainda não devolve endereço, contato nem fuso — o agregado Tenant
    // guarda um AddressJson opaco. Preencher isso exige estender o contrato do backend.
    oabState: '',
    email: '',
    phone: '',
    address: '',
    city: '',
    timezone: '',
  }
}

export async function updateTenantProfile(values: TenantProfileValues): Promise<TenantProfile> {
  if (env.useMocks) {
    tenantProfile = { ...values }
    return mockResponse(tenantProfile, 400)
  }
  const { data: me } = await apiClient.get<MeDto>(endpoints.status.me)
  if (!me.tenantId) {
    throw new Error('Sessão sem escritório identificado.')
  }

  await apiClient.put(endpoints.tenants.byId(me.tenantId), {
    name: values.name,
    logoUrl: null,
    addressJson: null,
  })

  return getTenantProfile()
}

export async function listUsers(): Promise<TeamUser[]> {
  if (env.useMocks) return mockResponse(users, 300)

  const { data } = await apiClient.get<ListUsersDto>(endpoints.users.list, {
    params: { page: 1, pageSize: 100 },
  })

  return data.items.map(toTeamUser)
}

export async function inviteUser(values: InviteUserValues): Promise<TeamUser> {
  if (env.useMocks) {
    const invited: TeamUser = {
      id: crypto.randomUUID(),
      name: values.email.split('@')[0] ?? values.email,
      email: values.email,
      role: values.role,
      status: 'invited',
      lastSignInAt: null,
    }
    users = [...users, invited]
    return mockResponse(invited, 400)
  }
  const { data } = await apiClient.post<TeamUser>(endpoints.users.invite, values)
  return data
}

export async function removeUser(id: string): Promise<void> {
  if (env.useMocks) {
    users = users.filter((u) => u.id !== id)
    await mockResponse(null, 400)
    return
  }
  await apiClient.delete(endpoints.users.byId(id))
}

export async function getNotificationPreferences(): Promise<NotificationPreference[]> {
  if (env.useMocks) return mockResponse(preferences, 250)
  return []
}

export async function updateNotificationPreferences(next: NotificationPreference[]) {
  if (env.useMocks) {
    preferences = structuredClone(next)
    return mockResponse(preferences, 400)
  }
  return next
}

export async function listIntegrations(): Promise<Integration[]> {
  return mockResponse(integrationsMock, 200)
}

export async function listAuditEntries(): Promise<AuditEntry[]> {
  if (env.useMocks) return mockResponse(auditEntriesMock, 350)

  const { data } = await apiClient.get<ListAuditLogsDto>(endpoints.audit.list, {
    params: { page: 1, pageSize: 50 },
  })

  return data.items.map(toAuditEntry)
}
