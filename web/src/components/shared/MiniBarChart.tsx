import { cn } from '@/lib/utils'

interface MiniBarChartProps {
  /** Série na ordem cronológica (mais antigo primeiro). */
  data: number[]
  /**
   * Descrição da série para leitores de tela. O gráfico é decorativo por si só —
   * o número que ele acompanha é sempre a informação principal.
   */
  label: string
  className?: string
}

const BAR_COUNT_MAX = 12

/**
 * Mini gráfico de barras (tendência) em CSS puro: sem SVG e sem biblioteca de
 * charts, para não pesar no bundle de uma visualização de 40px de altura.
 */
export function MiniBarChart({ data, label, className }: MiniBarChartProps) {
  const series = data.slice(-BAR_COUNT_MAX)
  if (series.length === 0) return null

  // Uma única passada para o máximo (evita sort/spread) — `|| 1` protege a divisão.
  let max = 0
  for (const value of series) if (value > max) max = value
  const scale = max || 1

  return (
    <div
      className={cn('flex h-10 items-end gap-0.5', className)}
      role="img"
      aria-label={label}
      title={label}
    >
      {series.map((value, index) => (
        <span
          key={index}
          className="min-h-0.5 w-1.5 flex-1 rounded-t-sm bg-secondary/25 last:bg-secondary"
          style={{ height: `${Math.max(6, (value / scale) * 100)}%` }}
        />
      ))}
    </div>
  )
}
