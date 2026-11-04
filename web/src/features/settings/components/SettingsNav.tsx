'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { settingsNavigation } from '@/config/navigation'
import { cn } from '@/lib/utils'

export function SettingsNav() {
  const pathname = usePathname()
  return (
    <nav
      aria-label="Seções de configurações"
      className="-mx-4 overflow-x-auto px-4 lg:mx-0 lg:px-0"
    >
      <ul className="flex gap-1 lg:flex-col">
        {settingsNavigation.map((item) => {
          const active = pathname === item.href
          return (
            <li key={item.href}>
              <Link
                href={item.href}
                aria-current={active ? 'page' : undefined}
                className={cn(
                  'block rounded-md px-3 py-2 text-sm whitespace-nowrap transition-colors',
                  active
                    ? 'bg-accent font-medium text-foreground'
                    : 'text-muted-foreground hover:bg-accent/60 hover:text-foreground'
                )}
              >
                {item.label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}
