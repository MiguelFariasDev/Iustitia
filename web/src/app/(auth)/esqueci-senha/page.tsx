import type { Metadata } from 'next'
import { ForgotPasswordForm } from '@/features/auth/components/ForgotPasswordForm'

export const metadata: Metadata = { title: 'Esqueci minha senha' }

export default function ForgotPasswordPage() {
  return <ForgotPasswordForm />
}
