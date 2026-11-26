import { env, isMockAuth } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'

/**
 * Avisa a API de que uma sessão acabou de abrir. O login acontece direto no Supabase Auth (a API nunca
 * vê a senha), então sem esta chamada o servidor não grava o último acesso nem audita o login.
 *
 * Nunca atrapalha a entrada: se falhar (rede, API fora do ar), a pessoa entra do mesmo jeito — o último
 * acesso ainda é preenchido pelo trigger do banco (migration 022), só a auditoria fica sem a linha.
 */
export async function registerSession(): Promise<void> {
  if (isMockAuth || env.useMocks) return

  try {
    await apiClient.post(endpoints.auth.session)
  } catch {
    // Silencioso de propósito: ver o comentário acima.
  }
}
