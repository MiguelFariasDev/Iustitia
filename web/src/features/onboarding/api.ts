import { z } from 'zod'
import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import type { AuthUser } from '@/lib/auth/types'
import { flowForRole, memberSteps, officeSteps, type OnboardingState } from './model'

/**
 * Persistência do onboarding.
 *  - Fluxo do escritório (Sócio/Titular): vai para as configurações do escritório
 *    (`PUT /settings`, chave `onboarding.office`) — compartilhado entre sócios e sobrevive a trocar
 *    de dispositivo. Espelhado no navegador (retomada instantânea e fallback se a API falhar).
 *  - Fluxo do convidado: `PUT /settings` exige Sócio, então o progresso por usuário fica SÓ no
 *    navegador. {{CONFIRMAR}}: endpoint `/me/onboarding` no backend elimina essa limitação.
 * Nada aqui é dado sensível: não há tokens, senhas nem documentos no estado.
 */
export const SETTING_KEY = 'onboarding.office'

const stepSchema = z.enum([...officeSteps, ...memberSteps])
const stateSchema = z.object({
  flow: z.enum(['office', 'member']),
  currentStep: stepSchema,
  completedSteps: z.array(stepSchema),
  skippedSteps: z.array(stepSchema),
  data: z.record(z.string(), z.unknown()),
  finishedAt: z.string().optional(),
  updatedAt: z.string(),
})

export function parseState(raw: unknown): OnboardingState | null {
  const parsed = stateSchema.safeParse(raw)
  return parsed.success ? (parsed.data as unknown as OnboardingState) : null
}

export function localKey(user: AuthUser): string {
  return flowForRole(user.role) === 'office'
    ? `iu-onboarding:office:${user.tenantId ?? user.id}`
    : `iu-onboarding:user:${user.id}`
}

function readLocal(user: AuthUser): OnboardingState | null {
  try {
    const raw = localStorage.getItem(localKey(user))
    return raw ? parseState(JSON.parse(raw)) : null
  } catch {
    return null
  }
}

function writeLocal(user: AuthUser, state: OnboardingState) {
  try {
    localStorage.setItem(localKey(user), JSON.stringify(state))
  } catch {
    // modo privado/sem espaço: o servidor (quando aplicável) continua sendo a fonte
  }
}

interface SettingsDto {
  items: { key: string; valueJson: string }[]
}

async function readRemote(): Promise<OnboardingState | null> {
  const { data } = await apiClient.get<SettingsDto>(endpoints.settings.root)
  const entry = data.items.find((item) => item.key === SETTING_KEY)
  if (!entry) return null
  try {
    return parseState(JSON.parse(entry.valueJson))
  } catch {
    return null
  }
}

export async function loadOnboarding(user: AuthUser): Promise<OnboardingState | null> {
  const local = readLocal(user)
  if (env.useMocks || flowForRole(user.role) !== 'office') return local

  try {
    const remote = await readRemote()
    // O mais recente vence: o navegador pode ter um passo que ainda não chegou ao servidor.
    if (remote && local) return remote.updatedAt >= local.updatedAt ? remote : local
    return remote ?? local
  } catch {
    return local
  }
}

/** Salva localmente e, no fluxo do escritório, no servidor. `synced=false` = só no navegador. */
export async function saveOnboarding(user: AuthUser, state: OnboardingState): Promise<{ synced: boolean }> {
  writeLocal(user, state)
  if (env.useMocks || state.flow !== 'office') return { synced: env.useMocks }

  try {
    await apiClient.put(endpoints.settings.root, { key: SETTING_KEY, valueJson: JSON.stringify(state) })
    return { synced: true }
  } catch {
    return { synced: false }
  }
}
