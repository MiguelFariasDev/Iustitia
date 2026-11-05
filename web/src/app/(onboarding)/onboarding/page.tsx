import type { Metadata } from 'next'
import { Suspense } from 'react'
import { OnboardingExperience } from '@/features/onboarding/components/OnboardingExperience'

export const metadata: Metadata = { title: 'Configuração inicial' }

export default function OnboardingPage() {
  return (
    // useSearchParams (?passo=) exige um limite de Suspense.
    <Suspense>
      <OnboardingExperience />
    </Suspense>
  )
}
