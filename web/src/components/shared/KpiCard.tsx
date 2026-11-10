import type { Route } from 'next'
import Link from 'next/link'
import { cn } from '@/lib/utils'

export type KpiTone = 'danger' | 'warning' | 'ia' | 'brand' | 'neutral'

/** Borda superior de 2px na cor do indicador — é o que diferencia os cartões do v4. */
const topBorder: Record<KpiTone, string> = {
  danger: 'border-t-destructive',
  warning: 'border-t-warning',
  ia: 'border-t-ia',
  brand: 'border-t-brand',
  neutral: 'border-t-line-strong',
}

const valueColor: Record<KpiTone, string> = {
  danger: 'text-destructive-foreground',
  warning: 'text-warning-foreground',
  ia: 'text-ia-text',
  brand: 'text-foreground',
  neutral: 'text-foreground',
}

interface KpiCardProps {
  label: string
  /** `null` = o dado não existe (módulo ainda sem backend): mostra "—" em vez de inventar. */
  value: number | string | null
  hint?: string
  tone?: KpiTone
  /** Cartão inteiro vira link (clique leva ao que o número conta). */
  href?: Route | string
  hrefLabel?: string
  /** Faixa compacta (variação B do painel): sem caixa, número de 31px. */
  compact?: boolean
  className?: string
}

export function KpiCard({
  label,
  value,
  hint,
  tone = 'neutral',
  href,
  hrefLabel,
  compact = false,
  className,
}: KpiCardProps) {
  const conteudo = (
    <>
      <p className="text-sm font-medium text-muted-foreground">{label}</p>
      <p
        className={cn(
          'font-medium tracking-tight',
          compact ? 'text-[31px] leading-9' : 'text-[45px] leading-[1.1]',
          value === null ? 'text-muted-foreground' : valueColor[tone]
        )}
      >
        {value === null ? '—' : value}
      </p>
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </>
  )

  if (compact) {
    return (
      <div className={cn('space-y-1 px-5 first:pl-0', className)}>
        {href ? (
          <Link
            href={href as never}
            aria-label={hrefLabel}
            className="block space-y-1 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {conteudo}
          </Link>
        ) : (
          conteudo
        )}
      </div>
    )
  }

  const caixa = cn(
    'block space-y-1.5 rounded-[10px] border border-t-2 bg-card p-4 shadow-sm',
    topBorder[tone],
    href && 'iu-lift outline-none focus-visible:ring-2 focus-visible:ring-ring',
    className
  )

  return href ? (
    <Link href={href as never} aria-label={hrefLabel} className={caixa}>
      {conteudo}
    </Link>
  ) : (
    <div className={caixa}>{conteudo}</div>
  )
}
