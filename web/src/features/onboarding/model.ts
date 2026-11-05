import type { UserRole } from '@/lib/auth/types'

/**
 * Modelo do onboarding: funções PURAS sobre o estado (testáveis sem React/rede).
 * Dois fluxos: `office` (Sócio/Titular: configura o escritório, 6 passos) e `member` (convidado:
 * 2 passos curtos). Regras: só o primeiro passo é obrigatório; o resto pode ser adiado
 * ("Fazer depois"); o progresso é salvo a cada passo; reenviar um passo não duplica nada.
 */
export type OnboardingFlow = 'office' | 'member'

export const officeSteps = ['office', 'monitoring', 'athena', 'team', 'processes', 'connections'] as const
export const memberSteps = ['profile', 'tour'] as const
export type OfficeStep = (typeof officeSteps)[number]
export type MemberStep = (typeof memberSteps)[number]
export type StepId = OfficeStep | MemberStep

export const stepLabels: Record<StepId, string> = {
  office: 'Seu escritório',
  monitoring: 'O que monitorar',
  athena: 'Como a Athena trabalha',
  team: 'Sua equipe',
  processes: 'Seus processos',
  connections: 'Conexões',
  profile: 'Seu perfil',
  tour: 'O que importa para você',
}

export interface MonitoredPerson {
  name: string
  /** Somente dígitos. */
  oabNumber: string
  oabState: string
}

export interface OnboardingData {
  office?: { name: string; timezone: string; language: 'pt-BR'; logoDataUrl?: string }
  monitoring?: { people: MonitoredPerson[]; tribunais: string[]; hour: string }
  /** `humanReview` NÃO é opção: é regra de produto, sempre ativa (ADR-012). */
  athena?: { suggestDeadlines: boolean; notifyUnlinked: boolean; alertLeadDays: 1 | 3 | 7 }
  /** Dados do cadastro (rascunho local) ou perguntados aqui quando faltam. */
  signup?: { oabNumber: string; oabState: string; teamSize: string; practiceArea: string }
  consent?: { termsAcceptedAt: string; termsVersion: string }
  invites?: { email: string; role: string }[]
  processes?: 'csv' | 'manual' | 'later'
  profile?: { displayName: string; oabNumber?: string; oabState?: string }
  mfaEnabled?: boolean
}

export interface OnboardingState {
  flow: OnboardingFlow
  currentStep: StepId
  completedSteps: StepId[]
  skippedSteps: StepId[]
  data: OnboardingData
  /** Preenchido ao chegar ao fim do fluxo. */
  finishedAt?: string
  updatedAt: string
}

export function stepsOf(flow: OnboardingFlow): readonly StepId[] {
  return flow === 'office' ? officeSteps : memberSteps
}

/** Titular e Sócio configuram o escritório; os demais perfis entram pelo fluxo curto. */
export function flowForRole(role: UserRole | null): OnboardingFlow {
  return role === 'Owner' || role === 'Partner' ? 'office' : 'member'
}

export function emptyState(flow: OnboardingFlow, now = new Date()): OnboardingState {
  return {
    flow,
    currentStep: stepsOf(flow)[0]!,
    completedSteps: [],
    skippedSteps: [],
    data: {},
    updatedAt: now.toISOString(),
  }
}

/** O primeiro passo é obrigatório; os demais podem ser adiados. */
export function canSkip(state: OnboardingState, step: StepId): boolean {
  return stepsOf(state.flow)[0] !== step
}

const unique = <T,>(items: T[]) => [...new Set(items)]

function indexOfStep(state: OnboardingState, step: StepId) {
  return stepsOf(state.flow).indexOf(step)
}

/** Próximo passo depois de `step` (ou null se era o último). */
export function stepAfter(state: OnboardingState, step: StepId): StepId | null {
  return stepsOf(state.flow)[indexOfStep(state, step) + 1] ?? null
}

export function stepBefore(state: OnboardingState, step: StepId): StepId | null {
  const index = indexOfStep(state, step)
  return index > 0 ? (stepsOf(state.flow)[index - 1] ?? null) : null
}

function advance(state: OnboardingState, step: StepId, now: Date): OnboardingState {
  const next = stepAfter(state, step)
  return {
    ...state,
    currentStep: next ?? step,
    finishedAt: next ? state.finishedAt : (state.finishedAt ?? now.toISOString()),
    updatedAt: now.toISOString(),
  }
}

/** Conclui o passo (idempotente: concluir duas vezes não duplica nada) e avança. */
export function completeStep(
  state: OnboardingState,
  step: StepId,
  patch: Partial<OnboardingData> = {},
  now = new Date()
): OnboardingState {
  return advance(
    {
      ...state,
      data: { ...state.data, ...patch },
      completedSteps: unique([...state.completedSteps, step]),
      skippedSteps: state.skippedSteps.filter((s) => s !== step),
    },
    step,
    now
  )
}

/** "Fazer depois": registra o adiamento e avança. Não pula o passo obrigatório. */
export function skipStep(state: OnboardingState, step: StepId, now = new Date()): OnboardingState {
  if (!canSkip(state, step)) return state
  return advance(
    {
      ...state,
      completedSteps: state.completedSteps.filter((s) => s !== step),
      skippedSteps: unique([...state.skippedSteps, step]),
    },
    step,
    now
  )
}

/** Atualiza dados sem avançar (ex.: convite enviado dentro do passo "equipe"). */
export function patchData(state: OnboardingState, patch: Partial<OnboardingData>, now = new Date()): OnboardingState {
  return { ...state, data: { ...state.data, ...patch }, updatedAt: now.toISOString() }
}

export function goToStep(state: OnboardingState, step: StepId, now = new Date()): OnboardingState {
  return indexOfStep(state, step) < 0 ? state : { ...state, currentStep: step, updatedAt: now.toISOString() }
}

/** Primeiro passo ainda não concluído nem adiado (retomada de onde parou). */
export function firstPendingStep(state: OnboardingState): StepId | null {
  return (
    stepsOf(state.flow).find((s) => !state.completedSteps.includes(s) && !state.skippedSteps.includes(s)) ?? null
  )
}

export function isFinished(state: OnboardingState | null): boolean {
  return Boolean(state?.finishedAt)
}

/** "3 de 6": passos efetivamente concluídos (adiados não contam). */
export function progress(state: OnboardingState) {
  return { done: state.completedSteps.length, total: stepsOf(state.flow).length }
}

export interface ChecklistItem {
  id: 'processes' | 'team' | 'mfa' | 'whatsapp'
  label: string
  done: boolean
  href: string
  /** Recurso que ainda não existe no produto: aparece, mas não conta no progresso. */
  soon?: boolean
}

export function checklist(state: OnboardingState | null): ChecklistItem[] {
  const data = state?.data ?? {}
  return [
    {
      id: 'processes',
      label: 'Adicionar processos',
      done: data.processes === 'csv' || data.processes === 'manual',
      href: '/processos/importar',
    },
    {
      id: 'team',
      label: 'Convidar a equipe',
      done: (data.invites?.length ?? 0) > 0,
      href: '/configuracoes/usuarios',
    },
    { id: 'mfa', label: 'Ativar a verificação em duas etapas', done: data.mfaEnabled === true, href: '/onboarding?passo=connections' },
    { id: 'whatsapp', label: 'Conectar o WhatsApp', done: false, href: '/configuracoes/integracoes', soon: true },
  ]
}
