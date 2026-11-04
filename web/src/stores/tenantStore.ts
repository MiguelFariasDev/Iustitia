import { create } from 'zustand'

/** Espelha Platform.Domain.FeatureFlags.FeatureFlagKeys. */
export const featureFlagKeys = {
  legalModule: 'legal.module',
  workflowModule: 'workflow.module',
  aiClassification: 'ai.classification',
  athenaAssistant: 'athena.assistant',
  publicApi: 'public.api',
  whatsAppNotifications: 'whatsapp.notifications',
  googleCalendarSync: 'google.calendar.sync',
  kanbanBoard: 'kanban.board',
  financialModule: 'financial.module',
} as const

export type FeatureFlagKey = (typeof featureFlagKeys)[keyof typeof featureFlagKeys]

export interface Tenant {
  id: string
  name: string
  slug: string
  plan: 'trial' | 'essencial' | 'profissional'
}

export interface TenantSettings {
  timezone: string
  locale: string
  businessHoursStart: string
  businessHoursEnd: string
}

interface TenantState {
  currentTenant: Tenant | null
  tenantSettings: TenantSettings | null
  featureFlags: Partial<Record<FeatureFlagKey, boolean>>
  setTenant: (tenant: Tenant | null, settings?: TenantSettings | null) => void
  setFeatureFlags: (flags: Partial<Record<FeatureFlagKey, boolean>>) => void
  isFeatureEnabled: (key: FeatureFlagKey) => boolean
  clear: () => void
}

export const useTenantStore = create<TenantState>()((set, get) => ({
  currentTenant: null,
  tenantSettings: null,
  featureFlags: {},
  setTenant: (currentTenant, tenantSettings = null) => set({ currentTenant, tenantSettings }),
  setFeatureFlags: (featureFlags) => set({ featureFlags }),
  // Fail-closed, como no backend (ADR-038): ausência de flag = desabilitada.
  isFeatureEnabled: (key) => get().featureFlags[key] === true,
  clear: () => set({ currentTenant: null, tenantSettings: null, featureFlags: {} }),
}))
