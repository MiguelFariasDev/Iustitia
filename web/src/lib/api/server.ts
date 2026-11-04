import 'server-only'
import { createSupabaseServerClient } from '@/lib/supabase/server'
import { createApiClient } from './client'

/**
 * Cliente da API para Server Components: o token vem dos cookies do Supabase da requisição
 * atual (nunca do singleton do navegador). Criar um por requisição.
 */
export async function createServerApiClient() {
  const supabase = await createSupabaseServerClient()
  return createApiClient({
    getAccessToken: async () =>
      (await supabase.auth.getSession()).data.session?.access_token ?? null,
    refreshAccessToken: async () => null,
    onUnauthorized: () => undefined,
  })
}
