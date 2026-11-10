'use client'

import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { routes } from '@/config/routes'
import { useAuth } from '@/hooks/useAuth'
import { userRoleLabels } from '@/lib/auth/types'
import { getInitials } from '@/lib/format'
import { useTenantStore } from '@/stores/tenantStore'
import { cn } from '@/lib/utils'
import { useUiStore } from '@/stores/uiStore'
import { SidebarNav } from './SidebarNav'

export function Sidebar() {
  const collapsed = useUiStore((s) => s.sidebarCollapsed)
  const toggleSidebar = useUiStore((s) => s.toggleSidebar)
  const { user } = useAuth()
  const tenantName = useTenantStore((s) => s.currentTenant?.name)
  const name = user?.name ?? 'Usuário'

  return (
    <aside
      aria-label="Barra lateral"
      className={cn(
        'sticky top-0 hidden h-dvh shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground transition-[width] duration-200 lg:flex',
        collapsed ? 'w-17' : 'w-[236px]'
      )}
    >
      <div
        className={cn(
          'flex min-h-16 flex-col justify-center gap-0.5 border-b border-sidebar-border px-4 py-3',
          collapsed && 'items-center px-0'
        )}
      >
        <Link
          href={routes.dashboard}
          className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring"
        >
          <Logo variant="inverted" showWordmark={!collapsed} />
        </Link>
        {tenantName && !collapsed ? (
          <p className="truncate pl-8 text-xs text-sidebar-muted">{tenantName}</p>
        ) : null}
      </div>
      <div className="flex-1 overflow-y-auto">
        <SidebarNav collapsed={collapsed} />
      </div>
      <div className="space-y-2 border-t border-sidebar-border p-3">
        <div className={cn('flex items-center gap-3 px-1', collapsed && 'justify-center px-0')}>
          <Avatar className="size-9 border-2 border-sidebar-ia">
            {user?.avatarUrl ? <AvatarImage src={user.avatarUrl} alt="" /> : null}
            <AvatarFallback className="bg-sidebar-accent text-xs font-semibold text-sidebar-accent-foreground">
              {getInitials(name)}
            </AvatarFallback>
          </Avatar>
          {collapsed ? null : (
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-sidebar-foreground">{name}</p>
              <p className="truncate text-xs text-sidebar-muted">
                {user?.role ? userRoleLabels[user.role] : 'Perfil não identificado'}
              </p>
            </div>
          )}
        </div>
        <button
          type="button"
          onClick={toggleSidebar}
          aria-expanded={!collapsed}
          aria-label={collapsed ? 'Expandir barra lateral' : 'Recolher barra lateral'}
          className={cn(
            'flex h-9 w-full items-center gap-3 rounded-md px-3 text-sm text-sidebar-muted outline-none hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground focus-visible:ring-2 focus-visible:ring-sidebar-ring',
            collapsed && 'justify-center px-0'
          )}
        >
          {collapsed ? (
            <PanelLeftOpen className="size-4.5" aria-hidden />
          ) : (
            <PanelLeftClose className="size-4.5" aria-hidden />
          )}
          {collapsed ? null : <span>Recolher</span>}
        </button>
      </div>
    </aside>
  )
}
