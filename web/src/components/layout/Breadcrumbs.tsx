'use client'

import type { Route } from 'next'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { segmentLabels } from '@/config/navigation'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'

export function Breadcrumbs({ className }: { className?: string }) {
  const pathname = usePathname()
  const segments = pathname.split('/').filter(Boolean)
  if (segments.length === 0) return null

  const crumbs = [
    // Raiz: o nome do produto (Marca Desacoplada), que leva ao painel — "Iustitia / <tela>".
    { href: routes.dashboard as Route, label: env.appName, linkable: true },
    ...segments.map((segment, index) => ({
      href: `/${segments.slice(0, index + 1).join('/')}` as Route,
      label: segmentLabels[segment] ?? decodeURIComponent(segment),
      // "/configuracoes" não é uma página própria; não vira link.
      linkable: segment !== 'configuracoes',
    })),
  ]

  return (
    <nav aria-label="Trilha de navegação" className={cn('min-w-0', className)}>
      <ol className="flex items-center gap-1.5 text-sm text-muted-foreground">
        {crumbs.map((crumb, index) => {
          const last = index === crumbs.length - 1
          return (
            <li key={`${index}-${crumb.href}`} className="flex min-w-0 items-center gap-1.5">
              {index > 0 ? (
                <span className="shrink-0 text-muted-foreground/60" aria-hidden>
                  /
                </span>
              ) : null}
              {last ? (
                <span aria-current="page" className="truncate font-medium text-foreground">
                  {crumb.label}
                </span>
              ) : crumb.linkable ? (
                <Link href={crumb.href} className="truncate rounded hover:text-foreground">
                  {crumb.label}
                </Link>
              ) : (
                <span className="truncate">{crumb.label}</span>
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
