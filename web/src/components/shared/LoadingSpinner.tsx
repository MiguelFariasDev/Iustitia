import { Loader2 } from 'lucide-react'
import { cn } from '@/lib/utils'

interface LoadingSpinnerProps {
  label?: string
  size?: 'sm' | 'md' | 'lg'
  className?: string
  /** Mostra o rótulo visualmente (sempre presente para leitores de tela). */
  showLabel?: boolean
  /**
   * Spinner puramente decorativo: sem `role="status"` nem rótulo acessível.
   * Use dentro de botões/elementos que já anunciam o próprio estado — evita
   * criar uma segunda região `status` competindo com a da página.
   */
  decorative?: boolean
}

const sizes = { sm: 'size-4', md: 'size-6', lg: 'size-10' } as const

export function LoadingSpinner({
  label = 'Carregando…',
  size = 'md',
  className,
  showLabel = false,
  decorative = false,
}: LoadingSpinnerProps) {
  if (decorative) {
    return (
      <Loader2 className={cn('animate-spin text-current', sizes[size], className)} aria-hidden />
    )
  }

  return (
    <div
      role="status"
      aria-live="polite"
      className={cn('inline-flex items-center gap-2', className)}
    >
      <Loader2 className={cn('animate-spin text-muted-foreground', sizes[size])} aria-hidden />
      <span className={showLabel ? 'text-sm text-muted-foreground' : 'sr-only'}>{label}</span>
    </div>
  )
}
