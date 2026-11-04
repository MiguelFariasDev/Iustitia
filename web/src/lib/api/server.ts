import 'server-only'
import { isMockAuth } from '@/config/env'
import { createSupabaseServerClient } from '@/lib/supabase/server'
import { createApiClient } from './client'

/**
 * Cliente da API para Server Components: o token vem dos cookies do Supabase da requisição
 * atual (nunca do singleton do navegador). Criar um por requisição.
 *
 * Em modo de autenticação simulado (`NEXT_PUBLIC_AUTH_MODE=mock`, usado pelo E2E e por demos
 * sem Supabase) não há projeto Supabase para consultar: criar o cliente lançaria erro por
 * falta de URL/chave. O token é nulo e os dados vêm dos mocks da API.
 */
export async function createServerApiClient() {
  if (isMockAuth) {
    return createApiClient({
      getAccessToken: async () => null,
      refreshAccessToken: async () => null,
      onUnauthorized: () => undefined,
    })
  }

  const supabase = await createSupabaseServerClient()
  return createApiClient({
    getAccessToken: async () =>
      (await supabase.auth.getSession()).data.session?.access_token ?? null,
    refreshAccessToken: async () => null,
    onUnauthorized: () => undefined,
  })
}
