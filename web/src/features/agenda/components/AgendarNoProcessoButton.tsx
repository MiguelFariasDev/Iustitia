'use client'

import { CalendarPlus } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { EventoDialog, type EventoDialogState } from './EventoDialog'

/**
 * "Agendar" na tela do processo: abre o mesmo diálogo da agenda, já com o processo escolhido.
 * Assim um prazo ou audiência nasce vinculado, sem o usuário sair da tela do caso.
 */
export function AgendarNoProcessoButton({ processoId }: { processoId: string }) {
  const [state, setState] = useState<EventoDialogState | null>(null)

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setState({ processoId })}>
        <CalendarPlus aria-hidden />
        Agendar
      </Button>
      <EventoDialog state={state} onClose={() => setState(null)} />
    </>
  )
}
