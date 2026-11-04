import type { Metadata } from 'next'
import { Suspense } from 'react'
import { LoginForm } from '@/features/auth/components/LoginForm'

export const metadata: Metadata = { title: 'Entrar' }

export default function LoginPage() {
  return (
    // useSearchParams (?next=) exige um limite de Suspense.
    <Suspense>
      <LoginForm />
    </Suspense>
  )
}
