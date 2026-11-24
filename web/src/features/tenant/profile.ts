import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'

/** Nome cadastrado do usuário no servidor; `null` se não der para obter (mantém o atual). */
export async function getProfileName(userId: string): Promise<string | null> {
  if (env.useMocks) return null
  try {
    const { data } = await apiClient.get<{ name?: string }>(endpoints.users.byId(userId))

    return data.name?.trim() || null
  } catch {
    return null
  }
}
