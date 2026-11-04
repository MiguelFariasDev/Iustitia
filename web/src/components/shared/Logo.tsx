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

/** Monograma "I" + balança. A marca vem de configuração (Marca Desacoplada — ADR-023). */
export function Logo({
  showWordmark = true,
  variant = 'default',
  size = 32,
  className,
}: LogoProps) {
  const name = env.appName

  return (
    <span className={cn('inline-flex items-center gap-2.5', className)}>
      <svg
        width={size}
        height={size}
        viewBox="0 0 32 32"
        role={showWordmark ? undefined : 'img'}
        aria-hidden={showWordmark ? true : undefined}
        aria-label={showWordmark ? undefined : name}
        className="shrink-0"
      >
        <rect
          x="0.5"
          y="0.5"
          width="31"
          height="31"
          rx="8"
          fill="#0A2540"
          stroke="#C9A961"
          strokeOpacity={variant === 'inverted' ? 0.6 : 0.25}
        />
        {/* Balança */}
        <path d="M6.5 11h19" stroke="#FFFFFF" strokeWidth="1.4" strokeLinecap="round" />
        <path
          d="M7 11.2 4.6 17.2M7 11.2l2.4 6M25 11.2l-2.4 6M25 11.2l2.4 6"
          stroke="#FFFFFF"
          strokeOpacity="0.75"
          strokeWidth="0.9"
        />
        <path d="M4 17.2h6a3 3 0 0 1-6 0ZM22 17.2h6a3 3 0 0 1-6 0Z" fill="#FFFFFF" />
        {/* Monograma "I" */}
        <path d="M12 7.5h8v1.8h-3v13.4h3v1.8h-8v-1.8h3V9.3h-3Z" fill="#C9A961" />
      </svg>
      {showWordmark ? (
        <span
          className={cn(
            'font-serif text-lg leading-none font-semibold tracking-wide',
            variant === 'inverted' ? 'text-white' : 'text-foreground'
          )}
        >
          {name}
        </span>
      ) : null}
    </span>
  )
}
