import { describe, expect, it } from 'vitest'
import {
  canSkip,
  checklist,
  completeStep,
  emptyState,
  firstPendingStep,
  flowForRole,
  goToStep,
  isFinished,
  patchData,
  progress,
  skipStep,
  stepAfter,
  stepBefore,
} from './model'

describe('flowForRole', () => {
  it('Titular e Sócio configuram o escritório; os demais entram pelo fluxo curto', () => {
    expect(flowForRole('Owner')).toBe('office')
    expect(flowForRole('Partner')).toBe('office')
    for (const role of ['Lawyer', 'Intern', 'Admin', 'Financial'] as const) {
      expect(flowForRole(role)).toBe('member')
    }
    expect(flowForRole(null)).toBe('member')
  })
})

describe('onboarding do escritório', () => {
  it('começa no primeiro passo, que NÃO pode ser adiado', () => {
    const state = emptyState('office')
    expect(state.currentStep).toBe('office')
    expect(canSkip(state, 'office')).toBe(false)
    expect(skipStep(state, 'office')).toBe(state)
    expect(canSkip(state, 'monitoring')).toBe(true)
  })

  it('concluir avança; concluir de novo é idempotente (nada duplica)', () => {
    let state = emptyState('office')
    state = completeStep(state, 'office', { office: { name: 'Souza', timezone: 'America/Sao_Paulo', language: 'pt-BR' } })
    const again = completeStep(state, 'office')
    expect(state.currentStep).toBe('monitoring')
    expect(again.completedSteps).toEqual(['office'])
    expect(again.data.office?.name).toBe('Souza')
  })

  it('"Fazer depois" avança sem contar como concluído, e concluir depois remove o adiamento', () => {
    let state = completeStep(emptyState('office'), 'office')
    state = skipStep(state, 'monitoring')
    expect(state.skippedSteps).toEqual(['monitoring'])
    expect(state.currentStep).toBe('athena')
    expect(progress(state)).toEqual({ done: 1, total: 6 })

    state = completeStep(goToStep(state, 'monitoring'), 'monitoring')
    expect(state.skippedSteps).toEqual([])
    expect(progress(state).done).toBe(2)
  })

  it('retoma do primeiro passo pendente (nem concluído nem adiado)', () => {
    let state = completeStep(emptyState('office'), 'office')
    state = skipStep(state, 'monitoring')
    state = completeStep(state, 'athena')
    expect(firstPendingStep(state)).toBe('team')
  })

  it('termina ao concluir ou adiar o último passo', () => {
    let state = emptyState('office')
    for (const step of ['office', 'monitoring', 'athena', 'team', 'processes'] as const) {
      state = completeStep(state, step)
      expect(isFinished(state)).toBe(false)
    }
    state = skipStep(state, 'connections', new Date('2026-11-05T10:00:00Z'))
    expect(isFinished(state)).toBe(true)
    expect(state.finishedAt).toBe('2026-11-05T10:00:00.000Z')
  })

  it('navegação para trás e para passo inexistente', () => {
    const state = emptyState('office')
    expect(stepBefore(state, 'monitoring')).toBe('office')
    expect(stepBefore(state, 'office')).toBeNull()
    expect(stepAfter(state, 'connections')).toBeNull()
    expect(goToStep(state, 'tour')).toBe(state)
  })

  it('patchData grava sem avançar', () => {
    const state = patchData(emptyState('office'), { mfaEnabled: true })
    expect(state.currentStep).toBe('office')
    expect(state.data.mfaEnabled).toBe(true)
  })
})

describe('onboarding do convidado', () => {
  it('tem 2 passos curtos', () => {
    let state = emptyState('member')
    expect(state.currentStep).toBe('profile')
    state = completeStep(state, 'profile', { profile: { displayName: 'Bruno' } })
    expect(state.currentStep).toBe('tour')
    state = skipStep(state, 'tour')
    expect(isFinished(state)).toBe(true)
  })
})

describe('checklist de primeiros passos', () => {
  it('reflete o que foi feito e marca WhatsApp como "em breve"', () => {
    const items = checklist(
      patchData(emptyState('office'), { processes: 'csv', invites: [{ email: 'a@b.com', role: 'Lawyer' }] })
    )
    expect(items.find((i) => i.id === 'processes')?.done).toBe(true)
    expect(items.find((i) => i.id === 'team')?.done).toBe(true)
    expect(items.find((i) => i.id === 'mfa')?.done).toBe(false)
    expect(items.find((i) => i.id === 'whatsapp')?.soon).toBe(true)
  })

  it('"processos: depois" não conta como feito', () => {
    expect(checklist(patchData(emptyState('office'), { processes: 'later' })).find((i) => i.id === 'processes')?.done).toBe(false)
  })
})
