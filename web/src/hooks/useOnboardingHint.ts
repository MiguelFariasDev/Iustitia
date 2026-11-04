'use client'

import { useCallback } from 'react'
import { useUiStore, type OnboardingHintId } from '@/stores/uiStore'

export type { OnboardingHintId }

/**
 * Visibilidade de um aviso de primeiro acesso. Assina apenas o booleano derivado
 * (`rerender-derived-state`): mudar a dispensa de *outro* aviso não re-renderiza este.
 */
export function useOnboardingHint(id: OnboardingHintId) {
  const visible = useUiStore((s) => s.hydrated && !s.dismissedHints.includes(id))
  const dismissHint = useUiStore((s) => s.dismissHint)
  const dismiss = useCallback(() => dismissHint(id), [dismissHint, id])

  return { visible, dismiss }
}
