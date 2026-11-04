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
    'ai.classification': false,
    'athena.assistant': false,
  },
}

export async function getCurrentTenant(): Promise<CurrentTenant> {
  if (env.useMocks) return mockResponse(mockTenant, 200)
  const { data } = await apiClient.get<CurrentTenant>(endpoints.status.me)
  return data
}
