'use client'

import { ClipboardCheck, Headset, LayoutDashboard, type LucideIcon } from 'lucide-react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { env } from '@/config/env'
import { isActivePath } from '@/config/navigation'
import { routes } from '@/config/routes'
import { useAccess } from '@/hooks/useAccess'
import type { Screen } from '@/lib/auth/access'
import { cn } from '@/lib/utils'

interface Tab {
  label: string
  href: string
  icon: LucideIcon
  /** Tela da matriz de acesso; sem ela, é de todos (o painel). */
  screen: Screen | null
  /** A revisão da IA só existe com mocks (sem backend de IA): igual ao link do painel. */
  needsMocks?: boolean
}

/** As três tarefas de celular do v4: ver o dia, revisar o que a IA sugeriu, atender o cliente. */
const tabs: Tab[] = [
  { label: 'Painel', href: routes.dashboard, icon: LayoutDashboard, screen: null },
  {
    label: 'Revisão',
    href: '/publicacoes/revisao',
    icon: ClipboardCheck,
    screen: 'review',
    needsMocks: true,
  },
  { label: 'Atender', href: routes.serviceDesk, icon: Headset, screen: 'serviceDesk' },
]

/**
 * Barra inferior (só abaixo de `md`). Cada aba aparece só se a pessoa pode abrir a tela, pela
 * mesma matriz do menu; o resto da navegação continua no menu lateral (☰). `aria-current`
 * marca a aba ativa, e o alvo de toque tem 56px de altura.
 */
export function BottomNav() {
  const pathname = usePathname()
  const access = useAccess()

  const visiveis = tabs.filter(
    (t) =>
      (!t.needsMocks || env.useMocks) &&
      (t.screen === null || (access.isReady && access.can(t.screen)))
  )

  return (
    <nav
      aria-label="Navegação rápida"
      className="fixed inset-x-0 bottom-0 z-30 flex h-14 border-t bg-background md:hidden"
    >
      {visiveis.map(({ label, href, icon: Icon }) => {
        const ativa = isActivePath(pathname, href)

        return (
          <Link
            key={href}
            href={href as never}
            aria-current={ativa ? 'page' : undefined}
            className={cn(
              'flex flex-1 flex-col items-center justify-center gap-0.5 text-xs font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-inset',
              ativa ? 'text-brand' : 'text-muted-foreground'
            )}
          >
            <Icon className="size-5" aria-hidden />
            {label}
          </Link>
        )
      })}
    </nav>
  )
}
