import type { UserRole } from '@/lib/auth/types'

export interface Profile {
  name: string
  email: string
  phone: string
  oabNumber: string
  oabState: string
}

export interface TenantProfile {
  name: string
  cnpj: string
  oabState: string
  email: string
  phone: string
  address: string
  city: string
  timezone: string
}

export type TeamUserStatus = 'active' | 'invited' | 'suspended'

export interface TeamUser {
  id: string
  name: string
  email: string
  role: UserRole
  status: TeamUserStatus
  lastSignInAt: string | null
}

export type NotificationChannel = 'email' | 'push' | 'inApp'

export interface NotificationPreference {
  key: string
  label: string
  description: string
  channels: Record<NotificationChannel, boolean>
  /** Alertas críticos (prazos) não podem ser totalmente desligados. */
  locked?: boolean
}

export type IntegrationStatus = 'connected' | 'available' | 'soon'

export interface Integration {
  id: string
  name: string
  description: string
  status: IntegrationStatus
  category: 'Tribunais' | 'Inteligência artificial' | 'Produtividade' | 'Comunicação'
}

export interface AuditEntry {
  id: string
  occurredAt: string
  actor: string
  action: string
  entity: string
  ipAddress: string
}
