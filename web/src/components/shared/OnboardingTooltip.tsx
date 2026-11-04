'use client'

import { Sparkles, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useOnboardingHint, type OnboardingHintId } from '@/hooks/useOnboardingHint'

interface OnboardingTooltipProps {
  /** Identidade estável do aviso: controla a dispensa persistida. */
  id: OnboardingHintId
  title: string
  description: string
  /** Lado do card a partir do qual a seta aponta para o conteúdo relacionado. */
  arrow?: 'top' | 'bottom' | 'none'
  className?: string
}

const arrowClasses = {
  top: 'before:-top-1.5 before:left-8',
  bottom: 'before:-bottom-1.5 before:left-8',
  none: '',
} as const

/**
 * Aviso de primeiro acesso (padrão "product tour"), em azul de destaque.
 * Renderiza `null` depois de dispensado — e também antes de o estado persistido ser
 * reidratado, para não piscar um aviso que o usuário já fechou.
 */
export function OnboardingTooltip({
  id,
  title,
  description,
  arrow = 'top',
  className,
}: OnboardingTooltipProps) {
  const { visible, dismiss } = useOnboardingHint(id)
  if (!visible) return null

  return (
    <aside
      aria-labelledby={`hint-${id}-title`}
      className={cn(
        'relative max-w-xl rounded-xl bg-secondary p-4 text-secondary-foreground shadow-md',
        arrow !== 'none' &&
          'before:absolute before:size-3 before:rotate-45 before:bg-secondary before:content-[""]',
        arrowClasses[arrow],
        className
      )}
    >
      <div className="flex items-start gap-3">
        <Sparkles className="mt-0.5 size-4.5 shrink-0" aria-hidden />
        <div className="min-w-0 flex-1 space-y-1">
          <h2 id={`hint-${id}-title`} className="text-sm font-semibold">
            {title}
          </h2>
          <p className="text-sm text-secondary-foreground/90">{description}</p>
        </div>
        <button
          type="button"
          onClick={dismiss}
          aria-label="Fechar aviso"
          className="-mt-1 -mr-1 rounded-md p-1 text-secondary-foreground/80 transition-colors hover:bg-white/15 hover:text-secondary-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
        >
          <X className="size-4" aria-hidden />
        </button>
      </div>
      <div className="mt-3 flex justify-end">
        <Button
          type="button"
          size="sm"
          onClick={dismiss}
          className="bg-white text-secondary hover:bg-white/90"
        >
          Entendi
        </Button>
      </div>
    </aside>
  )
}
