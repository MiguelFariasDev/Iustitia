import type { Metadata } from 'next'

import { AuditSettings } from '@/features/settings/components/AuditSettings'

export const metadata: Metadata = { title: 'Auditoria' }

export default function AuditPage() {
  return <AuditSettings />
}
