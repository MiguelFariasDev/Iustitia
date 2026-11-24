import { env } from '@/config/env'
import { cn } from '@/lib/utils'

interface LogoProps {
  /** Exibe o nome inteiro. Sem ele (barra lateral recolhida, topo do celular), só a inicial com o ponto. */
  showWordmark?: boolean
  /** Corpo da fonte em px. 26 na landing e no topo, 24 na barra lateral. */
  size?: number
  className?: string
}

/**
 * Wordmark do design system: o nome em Source Serif 4 600 com o ponto final em `brand`.
 * Ainda não existe símbolo oficial — não desenhe balança, martelo nem monograma. O nome vem de
 * configuração (Marca Desacoplada — ADR-023).
 */
export function Logo({ showWordmark = true, size = 26, className }: LogoProps) {
  const name = env.appName
  const classes = cn(
    'inline-flex items-baseline gap-px font-serif leading-none font-semibold tracking-[-0.01em] text-foreground select-none',
    className
  )
  const dot = (
    <b aria-hidden className="font-semibold text-brand">
      .
    </b>
  )

  if (!showWordmark) {
    return (
      <span role="img" aria-label={name} className={classes} style={{ fontSize: size }}>
        {name.charAt(0)}
        {dot}
      </span>
    )
  }

  return (
    <span className={classes} style={{ fontSize: size }}>
      {name}
      {dot}
    </span>
  )
}
