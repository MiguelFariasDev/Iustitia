import { TrendingDown, TrendingUp, type LucideIcon } from 'lucide-react'
import Link from 'next/link'
import type { Route } from 'next'
import { Card, CardContent } from '@/components/ui/card'
import { formatNumber } from '@/lib/format'
import { cn } from '@/lib/utils'
import { MiniBarChart } from './MiniBarChart'

interface MetricCardProps {
  label: string
  value: number
  delta?: number
  icon: LucideIcon
  /** Se um aumento é bom (processos ativos) ou ruim (prazos, pendências). */
  increaseIsGood?: boolean
  hint?: string
  /** Série histórica opcional: renderiza um mini gráfico de barras de tendência. */
  trend?: number[]
  trendLabel?: string
  /**
   * Para onde o cartão leva. Quando informado, o cartão inteiro vira um link — o número
   * responde à pergunta e o clique leva à lista que a responde em detalhe.
   */
  href?: Route | string
  /**
   * Nome acessível do link, quando o rótulo sozinho não diz o destino. Sem isso, um leitor
   * de tela anuncia só "Processos ativos" e não o que acontece ao clicar.
   */
  hrefLabel?: string
}

export function MetricCard({
  label,
  value,
  delta,
  icon: Icon,
  increaseIsGood = true,
  hint,
  trend,
  trendLabel,
  href,
  hrefLabel,
}: MetricCardProps) {
  const hasDelta = delta !== undefined && delta !== 0
  const up = (delta ?? 0) > 0
  const positive = up === increaseIsGood

  const conteudo = (
    <CardContent className="space-y-3 px-5">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm font-medium text-muted-foreground">{label}</p>
        <span className="flex size-8 items-center justify-center rounded-lg bg-secondary/10 text-secondary">
          <Icon className="size-4" aria-hidden />
        </span>
      </div>
      <div className="flex items-end justify-between gap-3">
        <p className="text-3xl font-semibold tracking-tight tabular">{formatNumber(value)}</p>
        {trend && trend.length > 0 ? (
          <MiniBarChart
            data={trend}
            label={trendLabel ?? `Tendência de ${label} nas últimas semanas`}
            className="w-24 shrink-0"
          />
        ) : null}
      </div>
      <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
        {hasDelta ? (
          <span
            className={cn(
              'inline-flex items-center gap-0.5 font-medium',
              positive ? 'text-success-foreground' : 'text-destructive-foreground'
            )}
          >
            {up ? (
              <TrendingUp className="size-3.5" aria-hidden />
            ) : (
              <TrendingDown className="size-3.5" aria-hidden />
            )}
            <span className="sr-only">{up ? 'Aumento de' : 'Redução de'} </span>
            {up ? '+' : '−'}
            {formatNumber(Math.abs(delta ?? 0))}
          </span>
        ) : null}
        <span>{hint ?? 'vs. semana anterior'}</span>
      </p>
    </CardContent>
  )

  if (!href) {
    return <Card className="gap-0 py-5">{conteudo}</Card>
  }

  // O link envolve o cartão inteiro (alvo grande, bom no toque) e carrega o foco visível —
  // sem ele, quem navega por teclado não enxerga onde está.
  return (
    <Card className="gap-0 py-5 transition-colors focus-within:border-secondary/40 hover:border-secondary/40">
      <Link
        href={href as never}
        aria-label={hrefLabel ?? label}
        className="rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        {conteudo}
      </Link>
    </Card>
  )
}
