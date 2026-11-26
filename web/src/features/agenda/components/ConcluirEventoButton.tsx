'use client'

import { Check, RotateCcw } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useConcluirEvento } from '../hooks'
import { estaConcluido, tipoEventoLabels, type AgendaEvento } from '../types'

interface ConcluirEventoButtonProps {
  evento: Pick<AgendaEvento, 'id' | 'titulo' | 'tipo' | 'concluidoEm'>
  className?: string
  /** Depois de concluir/reabrir com sucesso (ex.: fechar o diálogo em que o botão está). */
  onDone?: () => void
}

/**
 * Concluir (ou reabrir) um prazo, audiência, reunião ou tarefa da agenda. É um botão à parte,
 * nunca dentro de outro botão ou link: o nome acessível diz o que e qual item.
 */
export function ConcluirEventoButton({ evento, className, onDone }: ConcluirEventoButtonProps) {
  const { mutate, isPending } = useConcluirEvento()
  const concluido = estaConcluido(evento)
  const rotulo = tipoEventoLabels[evento.tipo].toLowerCase()

  return (
    <button
      type="button"
      disabled={isPending}
      onClick={() => mutate({ id: evento.id, concluido: !concluido }, { onSuccess: onDone })}
      aria-label={
        concluido ? `Reabrir ${rotulo}: ${evento.titulo}` : `Concluir ${rotulo}: ${evento.titulo}`
      }
      className={cn(
        'inline-flex h-7 shrink-0 items-center gap-1 rounded-md border px-2 text-xs font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50',
        concluido ? 'hover:bg-accent' : 'border-success/40 text-success hover:bg-success-soft',
        className
      )}
    >
      {concluido ? (
        <RotateCcw className="size-3.5" aria-hidden />
      ) : (
        <Check className="size-3.5" aria-hidden />
      )}
      {concluido ? 'Reabrir' : 'Concluir'}
    </button>
  )
}
