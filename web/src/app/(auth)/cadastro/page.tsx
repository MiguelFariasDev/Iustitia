import type { Metadata } from 'next'
import { SignupFlow } from '@/features/auth/components/SignupFlow'

export const metadata: Metadata = { title: 'Criar conta' }

export default function SignupPage() {
  return <SignupFlow />
}
