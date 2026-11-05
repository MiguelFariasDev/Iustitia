import { beforeEach, describe, expect, it } from 'vitest'
import type { AuthUser } from '@/lib/auth/types'
import { loadOnboarding, localKey, parseState, saveOnboarding } from './api'
import { completeStep, emptyState } from './model'

const owner: AuthUser = { id: 'u1', email: 'a@b.com', name: 'Ana', avatarUrl: null, role: 'Owner', tenantId: 't1' }
const lawyer: AuthUser = { ...owner, id: 'u2', role: 'Lawyer' }

describe('persistência do onboarding (modo mock → navegador)', () => {
  beforeEach(() => localStorage.clear())

  it('chaves separam escritório (por tenant) de convidado (por usuário)', () => {
    expect(localKey(owner)).toBe('iu-onboarding:office:t1')
    expect(localKey(lawyer)).toBe('iu-onboarding:user:u2')
  })

  it('salva e retoma exatamente de onde parou', async () => {
    const state = completeStep(emptyState('office'), 'office', {
      office: { name: 'Souza & Associados', timezone: 'America/Sao_Paulo', language: 'pt-BR' },
    })
    await saveOnboarding(owner, state)

    const loaded = await loadOnboarding(owner)
    expect(loaded?.currentStep).toBe('monitoring')
    expect(loaded?.completedSteps).toEqual(['office'])
    expect(loaded?.data.office?.name).toBe('Souza & Associados')
  })

  it('escritórios diferentes não compartilham progresso', async () => {
    await saveOnboarding(owner, emptyState('office'))
    expect(await loadOnboarding({ ...owner, tenantId: 't2' })).toBeNull()
  })

  it('descarta estado corrompido em vez de quebrar', async () => {
    localStorage.setItem(localKey(owner), '{"flow":"office","currentStep":"inexistente"}')
    expect(await loadOnboarding(owner)).toBeNull()
    localStorage.setItem(localKey(owner), 'não é json')
    expect(await loadOnboarding(owner)).toBeNull()
    expect(parseState(null)).toBeNull()
  })
})
