import { StatusBadge } from '@/components/shared/StatusBadge'
import { statusMeta } from '../presentation'
import type { PublicationStatus } from '../types'

/** O texto sempre acompanha a cor: o estado nunca depende só do tom para ser entendido. */
export function PublicacaoStatusBadge({ status }: { status: PublicationStatus }) {
  const meta = statusMeta[status]
  return <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
}
