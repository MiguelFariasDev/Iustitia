import { cn } from '@/lib/utils'
import { CIRCUNFERENCIA, deslocamento, type Progresso, type TomPrazo } from '../kanban'

const traco: Record<TomPrazo, string> = {
  ok: 'stroke-brand',
  atencao: 'stroke-ia',
  critico: 'stroke-destructive',
}

/**
 * Anel de progresso do prazo (38px, r = 15): quanto do tempo entre a criação e o prazo já passou.
 * O rótulo no centro ("2d", "0d", "1s", "ok") diz o que falta; a cor sozinha nunca é o único
 * sinal — o texto e o `aria-label` trazem o mesmo.
 */
export function AnelPrazo({ progresso }: { progresso: Progresso }) {
  const descricao = `${progresso.pct}% do prazo decorrido`

  return (
    <span
      role="img"
      aria-label={`Prazo: ${progresso.rotulo}, ${descricao}`}
      title={descricao}
      className="relative inline-flex size-[38px] shrink-0 items-center justify-center"
    >
      <svg width="38" height="38" viewBox="0 0 38 38" className="-rotate-90" aria-hidden>
        <circle cx="19" cy="19" r="15" fill="none" strokeWidth="3" className="stroke-border" />
        <circle
          cx="19"
          cy="19"
          r="15"
          fill="none"
          strokeWidth="3"
          strokeLinecap="round"
          strokeDasharray={CIRCUNFERENCIA}
          strokeDashoffset={deslocamento(progresso.pct)}
          className={cn(traco[progresso.tom])}
        />
      </svg>
      <span className="absolute font-mono text-[10px] font-medium" aria-hidden>
        {progresso.rotulo}
      </span>
    </span>
  )
}
