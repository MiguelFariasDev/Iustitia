export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'brand'

/** Fundo suave + texto de alto contraste (WCAG AA) + ponto sólido, por tom. */
export const toneClasses: Record<Tone, { badge: string; dot: string }> = {
  neutral: { badge: 'bg-muted text-muted-foreground border-border', dot: 'bg-muted-foreground' },
  info: { badge: 'bg-info/10 text-info-foreground border-info/25', dot: 'bg-info' },
  success: { badge: 'bg-success/10 text-success-foreground border-success/25', dot: 'bg-success' },
  warning: { badge: 'bg-warning-soft text-warning-foreground border-warning/30', dot: 'bg-warning' },
  danger: {
    badge: 'bg-destructive/10 text-destructive-foreground border-destructive/25',
    dot: 'bg-destructive',
  },
  /** `brand` é o tom da Athena (IA): verde-azulado, exclusivo da IA. */
  brand: { badge: 'bg-ia-soft text-ia-text border-ia-border', dot: 'bg-ia' },
}
