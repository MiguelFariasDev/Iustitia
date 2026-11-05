'use client'

import { Button } from '@/components/ui/button'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'

interface StepActionsProps {
  onBack?: () => void
  onSkip?: () => void
  continueLabel?: string
  busy?: boolean
  /** Para botões que não são `submit` (passos sem formulário). */
  onContinue?: () => void
  continueDisabled?: boolean
}

/** Voltar · Fazer depois · Continuar — igual em todos os passos. */
export function StepActions({
  onBack,
  onSkip,
  continueLabel = 'Continuar',
  busy,
  onContinue,
  continueDisabled,
}: StepActionsProps) {
  return (
    <div className="flex flex-wrap items-center gap-3 pt-2">
      {onBack ? (
        <Button type="button" variant="outline" className="h-11" onClick={onBack} disabled={busy}>
          Voltar
        </Button>
      ) : null}
      <Button
        type={onContinue ? 'button' : 'submit'}
        onClick={onContinue}
        className="iu-btn-lg"
        disabled={busy || continueDisabled}
        aria-busy={busy}
      >
        {busy ? <LoadingSpinner size="sm" decorative /> : null}
        {continueLabel}
      </Button>
      {onSkip ? (
        <Button type="button" variant="ghost" className="h-11" onClick={onSkip} disabled={busy}>
          Fazer depois
        </Button>
      ) : null}
    </div>
  )
}
