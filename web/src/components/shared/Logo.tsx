import { env } from '@/config/env'
import { cn } from '@/lib/utils'

interface LogoProps {
  /** Exibe o nome do produto (NEXT_PUBLIC_APP_NAME) ao lado do monograma. */
  showWordmark?: boolean
  /** `default` para fundos claros/tema; `inverted` para fundos escuros (ex.: sidebar). */
  variant?: 'default' | 'inverted'
  size?: number
  className?: string
}

/** Quadrado verde com brilho (v4). A marca vem de configuração (Marca Desacoplada — ADR-023). */
export function Logo({
  showWordmark = true,
  variant = 'default',
  size = 22,
  className,
}: LogoProps) {
  const name = env.appName

  return (
    <span className={cn('inline-flex items-center gap-2.5', className)}>
      <svg
        width={size}
        height={size}
        viewBox="0 0 22 22"
        role={showWordmark ? undefined : 'img'}
        aria-hidden={showWordmark ? true : undefined}
        aria-label={showWordmark ? undefined : name}
        className="shrink-0"
      >
        {/* Quadrado verde com o ícone de brilho (a marca é o mesmo nos dois temas) */}
        <rect width="22" height="22" rx="7" className="fill-logo" />
        <g
          transform="translate(4.2 4.2) scale(0.57)"
          fill="none"
          strokeWidth="2.3"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="stroke-logo-foreground"
        >
          <path d="M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z" />
          <path d="M20 3v4M22 5h-4M4 17v2M5 18H3" />
        </g>
      </svg>
      {showWordmark ? (
        <span
          className={cn(
            'text-lg leading-none font-semibold',
            variant === 'inverted' ? 'text-sidebar-foreground' : 'text-foreground'
          )}
        >
          {name}
        </span>
      ) : null}
    </span>
  )
}
