'use client'

import { useQuery } from '@tanstack/react-query'
import type { NavBadge } from '@/config/navigation'
import { pendingCountsQuery } from '@/features/counts/queries'
import { apiClient } from '@/lib/api/client'

/**
 * Contadores das badges da navegação. Usa `useQuery` (não suspense) de propósito:
 * a barra lateral nunca deve suspender o layout à espera de um número.
 */
export function useNavCounts(): Partial<Record<NavBadge, number>> {
  const { data } = useQuery(pendingCountsQuery(apiClient))
  return data ?? {}
}
