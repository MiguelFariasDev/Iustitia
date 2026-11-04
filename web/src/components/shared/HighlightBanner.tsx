import { Sparkles } from 'lucide-react'
import Link from 'next/link'
import type { Route } from 'next'
import { cn } from '@/lib/utils'

interface HighlightBannerProps {
  children: React.ReactNode
  action?: { label: string; href: Route }
  className?: string
}

/**
 * Faixa de destaque para novidades (tom informativo suave). Não é um `alert`:
 * é conteúdo promocional e não deve interromper leitores de tela.
 */
export function HighlightBanner({ children, action, className }: HighlightBannerProps) {
  return (
    <aside
      className={cn(
        'flex flex-col gap-2 rounded-xl border border-info/25 bg-info/8 px-4 py-3 sm:flex-row sm:items-center sm:gap-3',
        className
      )}
    >
      <Sparkles className="size-4.5 shrink-0 text-info" aria-hidden />
      <p className="min-w-0 flex-1 text-sm text-foreground">{children}</p>
      {action ? (
        <Link
          href={action.href}
          className="shrink-0 text-sm font-medium text-info-foreground underline-offset-4 hover:underline"
        >
          {action.label}
        </Link>
      ) : null}
    </aside>
  )
}
