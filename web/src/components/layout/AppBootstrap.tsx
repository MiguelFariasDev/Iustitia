'use client'

import { useQuery } from '@tanstack/react-query'
import { useAlertasRealtime } from '@/features/alertas/realtime'
import { useAtendimentosRealtime } from '@/features/atendimentos/realtime'
import { getCurrentTenant } from '@/features/tenant/api'
import { useGlobalHotkeys } from '@/hooks/useGlobalHotkeys'
import { queryKeys } from '@/lib/query/queryKeys'
import { getProfileName } from '@/features/tenant/profile'
import { useAuthStore } from '@/stores/authStore'
import { useTenantStore } from '@/stores/tenantStore'

/**
 * Carrega o contexto do escritório (tenant, flags), assina o canal de alertas em tempo real
 * e registra os atalhos globais. Não renderiza nada.
 */
export function AppBootstrap() {
  useGlobalHotkeys()

  useQuery({
    queryKey: queryKeys.tenant.current(),
    queryFn: async () => {
      const data = await getCurrentTenant()
      const store = useTenantStore.getState()
      store.setTenant(data.tenant, data.settings)
      store.setFeatureFlags(data.featureFlags ?? {})
      return data
    },
    staleTime: 5 * 60_000,
  })

  // O Supabase não guarda o nome: sem isto a saudação sairia "Bom dia, miguelfariasb8".
  const userId = useAuthStore((s) => s.user?.id)
  useQuery({
    queryKey: ['me', 'profile-name', userId],
    enabled: Boolean(userId),
    staleTime: 30 * 60_000,
    queryFn: async () => {
      const name = await getProfileName(userId!)
      if (name) useAuthStore.getState().setProfileName(userId!, name)

      return name
    },
  })

  // Notificação in-app em tempo real (canal privado do usuário no Supabase Realtime).
  useAlertasRealtime()

  // Atendimentos (WhatsApp) em tempo real: canal do escritório, só para quem pode ler conversas.
  useAtendimentosRealtime()

  return null
}
