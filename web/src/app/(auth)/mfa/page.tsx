import type { Metadata } from 'next'
import { Suspense } from 'react'
import { MfaForm } from '@/features/auth/components/MfaForm'

export const metadata: Metadata = { title: 'Verificação em duas etapas' }

export default function MfaPage() {
  return (
    // useSearchParams (?next=) exige um limite de Suspense.
    <Suspense>
      <MfaForm />
    </Suspense>
  )
}
