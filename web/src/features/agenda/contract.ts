import type { AgendaEvento, TipoEvento } from './types'

/** CalendarEventResponse (Workflow.Application.Features.Calendar). */
export interface CalendarEventDto {
  id: string
  title: string
  description: string | null
  type: string
  startAt: string
  endAt: string
  allDay: boolean
  location: string | null
  processId: string | null
  assignedUserId: string | null
}

export interface ListCalendarEventsDto {
  items: CalendarEventDto[]
}

const tipos: readonly TipoEvento[] = ['Prazo', 'Audiencia', 'Reuniao', 'Tarefa']

function toTipo(value: string): TipoEvento {
  return tipos.find((tipo) => tipo === value) ?? 'Tarefa'
}

export function toAgendaEvento(dto: CalendarEventDto): AgendaEvento {
  return {
    id: dto.id,
    titulo: dto.title,
    descricao: dto.description,
    tipo: toTipo(dto.type),
    inicio: dto.startAt,
    fim: dto.endAt,
    diaInteiro: dto.allDay,
    local: dto.location,
    processoId: dto.processId,
    responsavelId: dto.assignedUserId,
  }
}

/** Corpo de POST/PUT. `endAt` vazio deixa o servidor aplicar a duração padrão. */
export function toEventPayload(values: {
  titulo: string
  tipo: TipoEvento
  inicio: string
  fim: string | null
  diaInteiro: boolean
  descricao: string
  local: string
  processoId: string | null
  responsavelId: string | null
}) {
  const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim())

  return {
    title: values.titulo.trim(),
    type: values.tipo,
    startAt: values.inicio,
    endAt: values.fim,
    allDay: values.diaInteiro,
    description: blankToNull(values.descricao),
    location: blankToNull(values.local),
    processId: values.processoId,
    assignedUserId: values.responsavelId,
  }
}
