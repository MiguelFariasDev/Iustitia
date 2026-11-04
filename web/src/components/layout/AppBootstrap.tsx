'use client'

import { useQuery } from '@tanstack/react-query'
import { useAlertasRealtime } from '@/features/alertas/realtime'
import { getCurrentTenant } from '@/features/tenant/api'
import { useGlobalHotkeys } from '@/hooks/useGlobalHotkeys'
import { queryKeys } from '@/lib/query/queryKeys'
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
      store.setFeatureFlags(data.featureFlags)
      return data
    },
    staleTime: 5 * 60_000,
  })

  // Notificação in-app em tempo real (canal privado do usuário no Supabase Realtime).
  useAlertasRealtime()

  return null
}
