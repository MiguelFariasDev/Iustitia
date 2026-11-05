import type { AuthUser } from '@/lib/auth/types'
import type { OnboardingData, OnboardingState } from '../../model'

export interface StepProps {
  state: OnboardingState
  user: AuthUser
  /** Conclui o passo, gravando `patch` em `data`, e avança. */
  onComplete: (patch?: Partial<OnboardingData>) => void
  /** Grava dados sem avançar (ex.: convite enviado dentro do passo). */
  onPatch: (patch: Partial<OnboardingData>) => void
  onSkip?: () => void
  onBack?: () => void
}
