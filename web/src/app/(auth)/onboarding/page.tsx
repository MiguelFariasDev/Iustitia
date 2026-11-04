import type { Metadata } from 'next'
import { OnboardingWizard } from '@/features/auth/components/OnboardingWizard'

export const metadata: Metadata = { title: 'Cadastro do escritório' }

export default function OnboardingPage() {
  return <OnboardingWizard />
}
