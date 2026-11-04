'use client'

import { PanelLeftClose, PanelLeftOpen } from 'lucide-react'
import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import { useUiStore } from '@/stores/uiStore'
import { SidebarNav } from './SidebarNav'

export function Sidebar() {
  const collapsed = useUiStore((s) => s.sidebarCollapsed)
  const toggleSidebar = useUiStore((s) => s.toggleSidebar)

  return (
    <aside
      aria-label="Barra lateral"
      className={cn(
        'sticky top-0 hidden h-dvh shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground transition-[width] duration-200 lg:flex',
        collapsed ? 'w-17' : 'w-64'
      )}
    >
      <div
        className={cn(
          'flex h-16 items-center border-b border-sidebar-border px-4',
          collapsed && 'justify-center px-0'
        )}
      >
        <Link
          href={routes.dashboard}
          className="rounded-md outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring"
        >
          <Logo variant="inverted" showWordmark={!collapsed} />
        </Link>
      </div>
      <div className="flex-1 overflow-y-auto">
        <SidebarNav collapsed={collapsed} />
      </div>
      <div className="border-t border-sidebar-border p-3">
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
