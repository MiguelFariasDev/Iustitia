'use client'

import Link from 'next/link'
import { useSearchParams } from 'next/navigation'
import { useEffect } from 'react'
import { BrandMark } from '@/components/iu/BrandMark'
import { ThemeToggle } from '@/components/iu/ThemeToggle'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { useAuthStore } from '@/stores/authStore'
import { useStepTransition } from '@/features/auth/components/useStepTransition'
import {
  canSkip,
  completeStep,
  firstPendingStep,
  goToStep,
  isFinished,
  patchData,
  skipStep,
  stepBefore,
  stepLabels,
  stepsOf,
  type OnboardingData,
  type StepId,
} from '../model'
import { useOnboardingStore } from '../store'
import { CompletionScreen } from './CompletionScreen'
import { HelpCard } from './HelpCard'
import { StepAthena } from './steps/StepAthena'
import { StepConnections } from './steps/StepConnections'
import { StepMonitoring } from './steps/StepMonitoring'
import { StepOffice } from './steps/StepOffice'
import { StepProcesses } from './steps/StepProcesses'
import { StepTeam } from './steps/StepTeam'
import { MemberStepsMap } from './steps/memberMap'
import type { StepProps } from './steps/types'

type Phase = StepId | 'done'

interface StepDef {
  title: string
  description: string
  help: { title: string; body: React.ReactNode }
  Component: (props: StepProps) => React.ReactNode
}

const officeDefs: Record<string, StepDef> = {
  office: {
    title: 'Seu escritório',
    description: 'Confirme como o escritório aparece para a equipe.',
    help: { title: 'Por que pedimos isto', body: <p>O nome e o fuso identificam o escritório e definem como os prazos são calculados. O logotipo é opcional.</p> },
    Component: StepOffice,
  },
  monitoring: {
    title: 'O que a Athena deve monitorar',
    description: 'Cadastre os nomes e OABs que ela vai procurar no DJEN/CNJ.',
    help: { title: 'Por que pedimos isto', body: <p>As publicações são buscadas pelo nome e pela OAB, como o Diário as publica. Usamos só o necessário para encontrá-las; nada disso é compartilhado.</p> },
    Component: StepMonitoring,
  },
  athena: {
    title: 'Como a Athena deve trabalhar',
    description: 'Ajuste o que ela faz por conta própria. A decisão continua sendo sua.',
    help: { title: 'Revisão humana é regra', body: <p>O Provimento CFOAB nº 271/2025 e a nossa política exigem que o advogado decida. Por isso essa opção não existe como escolha.</p> },
    Component: StepAthena,
  },
  team: {
    title: 'Sua equipe',
    description: 'Convide quem trabalha com você e escolha o perfil de cada um.',
    help: { title: 'Cada perfil vê o que precisa', body: <p>O perfil limita o que a pessoa acessa. Os convites expiram em 7 dias e podem ser reenviados. Dá para fazer isto depois, em Configurações.</p> },
    Component: StepTeam,
  },
  processes: {
    title: 'Seus processos',
    description: 'Traga o que você já acompanha ou comece do zero.',
    help: { title: 'Por que pedimos isto', body: <p>Cada processo cadastrado passa a ter as publicações capturadas e vinculadas. Importe uma planilha ou cadastre um a um.</p> },
    Component: StepProcesses,
  },
  connections: {
    title: 'Conexões',
    description: 'Opcional: proteja a conta e conheça o que vem por aí.',
    help: { title: 'Segurança primeiro', body: <p>Advogados guardam sigilo de clientes. A verificação em duas etapas impede o acesso à conta mesmo que a senha seja descoberta.</p> },
    Component: StepConnections,
  },
}

const defs: Record<string, StepDef> = { ...officeDefs, ...MemberStepsMap }

/** Orquestrador de /onboarding: carrega o estado, escolhe o passo, persiste a cada avanço. */
export function OnboardingExperience() {
  const user = useAuthStore((s) => s.user)
  const authLoading = useAuthStore((s) => s.isLoading)
  const { status, state, load, update, synced } = useOnboardingStore()
  const search = useSearchParams()
  const { step: phase, leaving, goTo } = useStepTransition<Phase | null>(null)

  useEffect(() => {
    if (user) void load(user)
  }, [user, load])

  // Escolhe o passo inicial UMA vez: ?passo= válido, senão o primeiro pendente, senão o último.
  useEffect(() => {
    if (status !== 'ready' || !state || phase !== null) return
    const requested = search.get('passo') as StepId | null
    const valid = requested && stepsOf(state.flow).includes(requested)
    const target = valid ? requested : (firstPendingStep(state) ?? state.currentStep)
    goTo(isFinished(state) && !valid ? 'done' : target)
  }, [status, state, phase, search, goTo])

  if (authLoading || status !== 'ready' || !state || !user || phase === null) {
    return <OnboardingSkeleton />
  }

  const steps = stepsOf(state.flow)
  const current = phase === 'done' ? null : phase
  const def = current ? defs[current] : null

  function afterChange(next: OnboardingStateLike) {
    goTo(isFinished(next) ? 'done' : next.currentStep, () => document.getElementById('onboarding-heading')?.focus())
  }

  const stepProps = (step: StepId): StepProps => ({
    state,
    user,
    onComplete: (patch?: Partial<OnboardingData>) => {
      const next = update((s) => completeStep(s, step, patch))
      if (next) afterChange(next)
    },
    onPatch: (patch) => void update((s) => patchData(s, patch)),
    onSkip: canSkip(state, step)
      ? () => {
          const next = update((s) => skipStep(s, step))
          if (next) afterChange(next)
        }
      : undefined,
    onBack: stepBefore(state, step)
      ? () => {
          const previous = stepBefore(state, step)!
          update((s) => goToStep(s, previous))
          goTo(previous, () => document.getElementById('onboarding-heading')?.focus())
        }
      : undefined,
  })

  const index = current ? steps.indexOf(current) : steps.length - 1

  return (
    <div className="mx-auto w-full max-w-5xl px-4 py-6 sm:px-6">
      <header className="mb-8 flex items-center justify-between">
        <BrandMark className="text-lg" />
        <div className="flex items-center gap-2">
          <Link href={routes.dashboard} className="rounded-md px-2 py-2 text-sm text-muted-foreground hover:text-foreground">
            Sair e continuar depois
          </Link>
          <ThemeToggle />
        </div>
      </header>

      {current && def ? (
        <div className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_300px]">
          <div key={current} className={`iu-in ${leaving ? 'opacity-0 transition-opacity duration-[120ms]' : ''}`}>
            <p className="iu-mono mb-3 text-xs text-muted-foreground" aria-label={`Passo ${index + 1} de ${steps.length}`}>
              {String(index + 1).padStart(2, '0')} / {String(steps.length).padStart(2, '0')} · {stepLabels[current]}
            </p>
            <div
              role="progressbar"
              aria-label="Progresso do onboarding"
              aria-valuemin={1}
              aria-valuemax={steps.length}
              aria-valuenow={index + 1}
              className="mb-6 h-1 overflow-hidden rounded-full bg-border"
            >
              <div
                className="h-full origin-left rounded-full bg-[var(--iu-brand)] transition-transform duration-[250ms] ease-out"
                style={{ transform: `scaleX(${(index + 1) / steps.length})` }}
              />
            </div>
            <h1 id="onboarding-heading" tabIndex={-1} className="text-3xl font-semibold outline-none">
              {def.title}
            </h1>
            <p className="mt-2 mb-6 text-muted-foreground">{def.description}</p>
            {!synced ? (
              <p role="status" className="mb-4 text-xs text-muted-foreground">
                Progresso salvo neste navegador. Vamos sincronizar com o servidor assim que possível.
              </p>
            ) : null}
            <def.Component {...stepProps(current)} />
          </div>
          <HelpCard title={def.help.title}>{def.help.body}</HelpCard>
        </div>
      ) : (
        <div className={leaving ? 'opacity-0 transition-opacity duration-[120ms]' : undefined}>
          <h1 id="onboarding-heading" tabIndex={-1} className="sr-only outline-none">
            Configuração concluída
          </h1>
          <CompletionScreen state={state} />
        </div>
      )}
    </div>
  )
}

type OnboardingStateLike = NonNullable<ReturnType<typeof useOnboardingStore.getState>['state']>

function OnboardingSkeleton() {
  return (
    <div className="mx-auto w-full max-w-5xl space-y-4 px-4 py-10" aria-busy="true" aria-label="Carregando">
      <Skeleton className="iu-pulse h-4 w-40" />
      <Skeleton className="iu-pulse h-9 w-2/3" />
      <Skeleton className="iu-pulse h-64 w-full" />
    </div>
  )
}
