import { Check, Clock } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { stepLabels, stepsOf, type OnboardingState } from '../model'

/** Resumo final: ✓ o que foi feito, "Pendente" (com atalho) o que foi adiado. */
export function CompletionScreen({ state }: { state: OnboardingState }) {
  return (
    <div className="iu-in space-y-6">
      <div className="flex items-center gap-4">
        <span className="flex size-14 items-center justify-center rounded-full bg-[var(--iu-brand-soft)] text-[var(--iu-brand)]">
          <svg viewBox="0 0 24 24" className="iu-check size-8" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
        </span>
        <div>
          <h2 className="text-2xl font-semibold">Tudo pronto para começar</h2>
          <p className="text-sm text-muted-foreground">Você pode ajustar qualquer item depois, em Configurações.</p>
        </div>
      </div>

      <ul className="divide-y rounded-[14px] border bg-card" aria-label="Resumo da configuração">
        {stepsOf(state.flow).map((step) => {
          const done = state.completedSteps.includes(step)
          return (
            <li key={step} className="flex items-center gap-3 px-4 py-3 text-sm">
              {done ? (
                <Check className="size-4 text-[var(--iu-ok)]" aria-hidden />
              ) : (
                <Clock className="size-4 text-[var(--iu-warn)]" aria-hidden />
              )}
              <span className="flex-1">{stepLabels[step]}</span>
              {done ? (
                <span className="text-[var(--iu-ok)]">Concluído</span>
              ) : (
                <Link href={`/onboarding?passo=${step}` as never} className="text-[var(--iu-warn)] underline underline-offset-4">
                  Pendente · configurar
                </Link>
              )}
            </li>
          )
        })}
      </ul>

      <Button asChild className="iu-btn-lg">
        <Link href={routes.dashboard}>Ir para o Painel</Link>
      </Button>
    </div>
  )
}
