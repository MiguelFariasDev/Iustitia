'use client'

import { usePathname } from 'next/navigation'
import { AccessDenied } from '@/components/shared/AccessDenied'
import { useAccess } from '@/hooks/useAccess'
import { screenForPath } from '@/lib/auth/access'

/**
 * Barra a renderização de telas fora do alcance do usuário (matriz em lib/auth/access.ts).
 * Rotas que não estão na matriz passam. Enquanto o usuário não carrega, deixa passar: a
 * autenticação já foi exigida pelo proxy e os dados continuam protegidos pela API.
 */
export function AccessGuard({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const access = useAccess()
  const screen = screenForPath(pathname)

  if (screen && access.isReady && !access.can(screen)) return <AccessDenied />

  return children
}
