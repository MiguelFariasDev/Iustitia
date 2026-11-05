'use client'

import { LifeBuoy, Sparkles } from 'lucide-react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip'
import {
  filterNavigation,
  footerNavigation,
  isActivePath,
  mainNavigation,
  type NavItem,
} from '@/config/navigation'
import { useAccess } from '@/hooks/useAccess'
import { useNavCounts } from '@/hooks/useNavCounts'
import { notify } from '@/lib/notify'
import { cn } from '@/lib/utils'

interface SidebarNavProps {
  collapsed?: boolean
  onNavigate?: () => void
}

const itemBase =
  'flex h-9 items-center gap-3 rounded-md px-3 text-sm outline-none transition-colors focus-visible:ring-2 focus-visible:ring-sidebar-ring'

function CountBadge({ count, collapsed }: { count: number; collapsed: boolean }) {
  if (count <= 0) return null
  const text = count > 99 ? '99+' : String(count)

  return (
    <span
      className={cn(
        'ml-auto inline-flex min-w-5 items-center justify-center rounded-full bg-red-700 px-1.5 py-px text-[10px] font-semibold text-white tabular',
        collapsed && 'absolute top-1 right-1 ml-0 min-w-0 px-1 py-0 text-[9px] leading-tight'
      )}
    >
      {text}
      <span className="sr-only"> pendentes</span>
    </span>
  )
}

function ItemContent({
  item,
  collapsed,
  count,
}: {
  item: NavItem
  collapsed: boolean
  count?: number
}) {
  const Icon = item.icon
  return (
    <>
      <Icon className="size-4.5 shrink-0" aria-hidden />
      <span className={cn('truncate', collapsed && 'sr-only')}>{item.label}</span>
      {item.ai ? (
        <Sparkles
          className={cn('size-3.5 shrink-0 text-sidebar-ia', collapsed && 'hidden')}
          aria-label="Com inteligência artificial"
        />
      ) : null}
      {count === undefined ? null : <CountBadge count={count} collapsed={collapsed} />}
      {item.soon ? (
        collapsed ? (
          <span className="sr-only"> (em breve)</span>
        ) : (
          <span className="ml-auto rounded-full border border-sidebar-border px-1.5 py-px text-[10px] font-medium tracking-wide text-sidebar-muted uppercase">
            Em breve
          </span>
        )
      ) : item.demo ? (
        collapsed ? (
          <span className="sr-only"> (dados de demonstração)</span>
        ) : (
          <span className="ml-auto rounded-full border border-sidebar-border px-1.5 py-px text-[10px] font-medium tracking-wide text-sidebar-muted uppercase">
            Demo
          </span>
        )
      ) : null}
    </>
  )
}

export function SidebarNav({ collapsed = false, onNavigate }: SidebarNavProps) {
  const pathname = usePathname()
  const counts = useNavCounts()
  const access = useAccess()
  // Antes de o usuário carregar não há como decidir; mostra tudo por um instante em vez de piscar
  // um menu vazio. A API segue barrando o que o papel não pode.
  const navigation = access.isReady
    ? filterNavigation(mainNavigation, access.can, access.label)
    : mainNavigation

  return (
    <nav aria-label="Navegação principal" className="flex h-full flex-col px-3 py-4">
      <div className="flex flex-1 flex-col gap-5">
        {navigation.map((group, groupIndex) => (
          <div key={group.label ?? `group-${groupIndex}`} className="space-y-1">
            {group.label ? (
              <p
                className={cn(
                  'px-3 pb-1 text-[11px] font-semibold tracking-wider text-sidebar-muted uppercase',
                  collapsed && 'sr-only'
                )}
              >
                {group.label}
              </p>
            ) : null}
            <ul className="space-y-0.5">
              {group.items.map((item) => {
                const active = isActivePath(pathname, item.href)
                const count = item.badge ? counts[item.badge] : undefined

                const node = (
                  <Link
                    href={item.href}
                    onClick={onNavigate}
                    aria-current={active ? 'page' : undefined}
                    className={cn(
                      itemBase,
                      active
                        ? 'bg-sidebar-accent font-medium text-sidebar-accent-foreground shadow-[inset_2px_0_0_var(--sidebar-ring)]'
                        : 'text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
                      item.soon && !active && 'text-sidebar-muted',
                      collapsed && 'relative justify-center px-0'
                    )}
                  >
                    <ItemContent item={item} collapsed={collapsed} count={count} />
                  </Link>
                )

                return (
                  <li key={item.label}>
                    {collapsed ? (
                      <Tooltip>
                        <TooltipTrigger asChild>{node}</TooltipTrigger>
                        <TooltipContent side="right">
                          {item.label}
                          {item.soon ? ' · em breve' : ''}
                          {item.demo ? ' · demonstração' : ''}
                          {count ? ` · ${count} pendentes` : ''}
                        </TooltipContent>
                      </Tooltip>
                    ) : (
                      node
                    )}
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </div>

      <ul className="space-y-0.5 border-t border-sidebar-border pt-3">
        {footerNavigation.map((item) => {
          const active = isActivePath(pathname, item.href)
          return (
            <li key={item.label}>
              <Link
                href={item.href}
                onClick={onNavigate}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  itemBase,
                  active
                    ? 'bg-sidebar-accent font-medium text-sidebar-accent-foreground'
                    : 'text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
                  collapsed && 'justify-center px-0'
                )}
              >
                <ItemContent item={item} collapsed={collapsed} />
              </Link>
            </li>
          )
        })}
        <li>
          <button
            type="button"
            onClick={() => notify.info('A central de suporte chega junto com o lançamento.')}
            className={cn(
              itemBase,
              'w-full text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
              collapsed && 'justify-center px-0'
            )}
          >
            <LifeBuoy className="size-4.5 shrink-0" aria-hidden />
            <span className={cn('truncate', collapsed && 'sr-only')}>Suporte</span>
          </button>
        </li>
      </ul>
    </nav>
  )
}
