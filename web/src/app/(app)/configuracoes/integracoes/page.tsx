import type { Metadata } from 'next'

import { IntegrationsSettings } from '@/features/settings/components/IntegrationsSettings'

export const metadata: Metadata = { title: 'Integrações' }

export default function IntegrationsPage() {
  return <IntegrationsSettings />
}
