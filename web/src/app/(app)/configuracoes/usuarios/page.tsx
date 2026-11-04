import type { Metadata } from 'next'
import { Suspense } from 'react'
import { UsersSettings } from '@/features/settings/components/UsersSettings'

export const metadata: Metadata = { title: 'Usuários' }

export default function UsersPage() {
  return (
    // useSearchParams (?convidar=1) exige um limite de Suspense.
    <Suspense>
      <UsersSettings />
    </Suspense>
  )
}
