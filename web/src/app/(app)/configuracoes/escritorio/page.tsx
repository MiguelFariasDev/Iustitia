import type { Metadata } from 'next'

import { TenantSettings } from '@/features/settings/components/TenantSettings'

export const metadata: Metadata = { title: 'Escritório' }

export default function TenantSettingsPage() {
  return <TenantSettings />
}
