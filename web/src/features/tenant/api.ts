import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { mockResponse } from '@/lib/api/mock'
import type { FeatureFlagKey, Tenant, TenantSettings } from '@/stores/tenantStore'

export interface CurrentTenant {
  tenant: Tenant
  settings: TenantSettings
  featureFlags: Partial<Record<FeatureFlagKey, boolean>>
}

const mockTenant: CurrentTenant = {
  tenant: {
    id: '00000000-0000-4000-8000-0000000000aa',
    name: 'Souza & Associados Advocacia',
    slug: 'souza-associados',
    plan: 'trial',
  },
  settings: {
    timezone: 'America/Sao_Paulo',
    locale: 'pt-BR',
    businessHoursStart: '08:00',
    businessHoursEnd: '18:00',
  },
  featureFlags: {
    'legal.module': false,
    'workflow.module': false,
    'ai.classification': true,
    'athena.assistant': false,
  },
}

interface MeDto {
  tenantId: string | null
}

interface TenantDto {
  id: string
  name: string
}

interface FeatureFlagsDto {
  items: { key: string; isEnabled: boolean }[]
}

interface SettingsDto {
  items: { key: string; valueJson: string }[]
}

const DEFAULT_SETTINGS: TenantSettings = {
  timezone: 'America/Sao_Paulo',
  locale: 'pt-BR',
  businessHoursStart: '08:00',
  businessHoursEnd: '18:00',
}

/** Uma falha em UMA das consultas não pode derrubar o contexto inteiro: cai no valor neutro. */
async function tentar<T>(consulta: Promise<{ data: T }>, neutro: T): Promise<T> {
  try {
    return (await consulta).data
  } catch {
    return neutro
  }
}

function textoDe(valueJson: string | undefined): string | null {
  if (!valueJson) return null
  try {
    const valor: unknown = JSON.parse(valueJson)

    return typeof valor === 'string' && valor !== '' ? valor : null
  } catch {
    return null
  }
}

/**
 * Contexto do escritório. `/me` só traz o usuário (do JWT); o escritório, as configurações e as
 * flags vêm de endpoints próprios — por isso são três consultas, não uma. Sem flag cadastrada,
 * vale o padrão fail-closed (tudo desligado), como no backend (ADR-038).
 */
export async function getCurrentTenant(): Promise<CurrentTenant> {
  if (env.useMocks) return mockResponse(mockTenant, 200)

  const { data: me } = await apiClient.get<MeDto>(endpoints.status.me)
  const [tenant, flags, settings] = await Promise.all([
    me.tenantId
      ? tentar(apiClient.get<TenantDto>(endpoints.tenants.byId(me.tenantId)), null)
      : Promise.resolve(null),
    tentar(apiClient.get<FeatureFlagsDto>(endpoints.featureFlags.list), { items: [] }),
    tentar(apiClient.get<SettingsDto>(endpoints.settings.root), { items: [] }),
  ])

  const valores = new Map(settings.items.map((item) => [item.key, item.valueJson]))
  const nome = tenant?.name ?? ''

  return {
    tenant: {
      id: tenant?.id ?? me.tenantId ?? '',
      name: nome,
      slug: nome
        .normalize('NFD')
        .replace(/\p{Diacritic}/gu, '')
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-|-$/g, ''),
      plan: 'trial',
    },
    settings: {
      ...DEFAULT_SETTINGS,
      timezone: textoDe(valores.get('timezone')) ?? DEFAULT_SETTINGS.timezone,
      locale: textoDe(valores.get('locale')) ?? DEFAULT_SETTINGS.locale,
    },
    featureFlags: Object.fromEntries(flags.items.map((flag) => [flag.key, flag.isEnabled])),
  }
}
