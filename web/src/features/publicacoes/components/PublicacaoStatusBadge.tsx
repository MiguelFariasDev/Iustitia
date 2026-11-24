'use client'

import { Sparkles } from 'lucide-react'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { useTenantStore } from '@/stores/tenantStore'
import type { Publication } from '../types'
import { statusBadgeOf } from '../view'

/**
 * Selo de status da linha. O texto sempre acompanha a cor (e o ✨ da IA): o estado nunca depende
 * só do tom. Quem decide o que mostrar é `statusBadgeOf`, que não afirma "IA sugeriu prazo".
 */
export function PublicacaoStatusBadge({ publication }: { publication: Publication }) {
  const aiEnabled = useTenantStore((s) => s.featureFlags['ai.classification'] === true)
  const { label, tone, ai } = statusBadgeOf(publication, aiEnabled)

  return (
    <StatusBadge tone={tone} hideDot={ai}>
      {ai ? <Sparkles className="size-3 shrink-0" aria-hidden /> : null}
      {label}
    </StatusBadge>
  )
}
