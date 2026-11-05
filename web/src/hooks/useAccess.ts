'use client'

import { useMemo } from 'react'
import { canAccess, hasTeam, screenLabel, type AccessContext, type Screen } from '@/lib/auth/access'
import { useAuthStore } from '@/stores/authStore'

/**
 * Acesso do usuário logado às telas (ver lib/auth/access.ts). Conveniência de interface — a
 * API continua sendo quem autoriza de verdade.
 */
export function useAccess() {
  const role = useAuthStore((s) => s.user?.role ?? null)
  const accountType = useAuthStore((s) => s.user?.accountType ?? null)
  const hasUser = useAuthStore((s) => s.user !== null)

  return useMemo(() => {
    const context: AccessContext = { role, accountType }

    return {
      context,
      /** Sem usuário carregado ainda: não há como decidir, e esconder tudo piscaria o menu. */
      isReady: hasUser,
      can: (screen: Screen) => canAccess(screen, context),
      label: (screen: Screen, fallback: string) => screenLabel(screen, fallback, context),
      isSolo: accountType === 'Solo',
      hasTeam: hasTeam(context),
    }
  }, [role, accountType, hasUser])
}
