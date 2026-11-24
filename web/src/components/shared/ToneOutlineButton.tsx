import { cn } from '@/lib/utils'

export type OutlineTone = 'ok' | 'ia' | 'danger'

/**
 * Botão de DECISÃO do v4: contornado (borda 1,5px na cor semântica, fundo transparente) e, no
 * hover, preenchido com o tom suave. Nunca sólido — decisões jurídicas não devem ter um botão
 * "principal" que induza ao clique. Verde = aceitar, violeta = corrigir/editar, vermelho = rejeitar.
 */
const tones: Record<OutlineTone, string> = {
  ok: 'border-success text-success-foreground hover:bg-success-soft',
  ia: 'border-ia text-ia-text hover:bg-ia-soft',
  danger: 'border-destructive text-destructive-foreground hover:bg-destructive-soft',
}

export function ToneOutlineButton({
  tone,
  className,
  type = 'button',
  ...props
}: React.ComponentProps<'button'> & { tone: OutlineTone }) {
  return (
    <button
      type={type}
      className={cn(
        'inline-flex h-9 items-center justify-center gap-1.5 rounded-lg border-[1.5px] bg-transparent px-3.5 text-sm font-medium whitespace-nowrap transition-colors outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50',
        tones[tone],
        className
      )}
      {...props}
    />
  )
}
