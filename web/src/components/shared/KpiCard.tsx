import type { Route } from 'next'
import Link from 'next/link'
import { cn } from '@/lib/utils'

export type KpiTone = 'danger' | 'warning' | 'ia' | 'brand' | 'neutral'

const valueColor: Record<KpiTone, string> = {
  danger: 'text-destructive-foreground',
  warning: 'text-warning-foreground',
  ia: 'text-ia-text',
  brand: 'text-primary',
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
      <p className="iu-label text-muted-foreground">{label}</p>
      <p
        className={cn(
          compact ? 'text-[31px] leading-9 font-bold tracking-tight tabular' : 'iu-metric',
          value === null ? 'text-muted-foreground' : valueColor[tone]
        )}
      >
        {value === null ? '—' : value}
      </p>
      {hint ? <p className="text-[13px] leading-[19px] text-muted-foreground">{hint}</p> : null}
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
    'block space-y-2 rounded-lg border bg-card p-5 shadow-sm',
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
