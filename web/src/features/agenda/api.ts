import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import {
  toAgendaEvento,
  toEventPayload,
  type CalendarEventDto,
  type ListCalendarEventsDto,
} from './contract'
import {
  mockConcluirEvento,
  mockCreateEvento,
  mockDeleteEvento,
  mockListEventos,
  mockUpdateEvento,
} from './mocks'
import type { EventoFormValues } from './schemas'
import { formToPayload } from './schemas'
import type { AgendaEvento, AgendaPeriodo } from './types'

const notFound = () =>
  new ApiError(404, {
    errorCode: 'CALENDAR_EVENT_NOT_FOUND',
    detail: 'Evento da agenda não encontrado.',
  })

export async function listEventos(
  periodo: AgendaPeriodo,
  responsavelId: string | null = null
): Promise<AgendaEvento[]> {
  if (env.useMocks) return mockResponse(mockListEventos(periodo, responsavelId), 200)

  const { data } = await apiClient.get<ListCalendarEventsDto>(endpoints.agenda.eventos, {
    params: { from: periodo.from, to: periodo.to, responsavelId: responsavelId ?? undefined },
  })

  return data.items.map(toAgendaEvento)
}

/** Janela da tela do processo: 5 anos para trás e 5 para a frente (o servidor aceita até 3660 dias com filtro por processo). */
const JANELA_PROCESSO_DIAS = 1825

/** Prazos, audiências e demais compromissos de UM processo, do mais antigo ao mais recente. */
export async function listEventosDoProcesso(processoId: string): Promise<AgendaEvento[]> {
  const agora = Date.now()
  const dia = 24 * 60 * 60 * 1000
  const periodo: AgendaPeriodo = {
    from: new Date(agora - JANELA_PROCESSO_DIAS * dia).toISOString(),
    to: new Date(agora + JANELA_PROCESSO_DIAS * dia).toISOString(),
  }

  if (env.useMocks) {
    return mockResponse(
      mockListEventos(periodo).filter((evento) => evento.processoId === processoId),
      200
    )
  }

  const { data } = await apiClient.get<ListCalendarEventsDto>(endpoints.agenda.eventos, {
    params: { from: periodo.from, to: periodo.to, processoId },
  })

  return data.items.map(toAgendaEvento).sort((a, b) => a.inicio.localeCompare(b.inicio))
}

export async function createEvento(values: EventoFormValues): Promise<AgendaEvento> {
  const payload = formToPayload(values)

  if (env.useMocks) return mockResponse(mockCreateEvento(payload), 150)

  const { data } = await apiClient.post<CalendarEventDto>(
    endpoints.agenda.eventos,
    toEventPayload(payload)
  )

  return toAgendaEvento(data)
}

export async function updateEvento(id: string, values: EventoFormValues): Promise<void> {
  const payload = formToPayload(values)

  if (env.useMocks) {
    const fim =
      payload.fim ?? new Date(new Date(payload.inicio).getTime() + 3_600_000).toISOString()
    if (!mockUpdateEvento(id, { ...payload, fim })) throw notFound()

    await mockResponse(null, 150)
    return
  }

  await apiClient.put(endpoints.agenda.eventoById(id), toEventPayload(payload))
}

/** Arrastar e soltar: muda só o período (PATCH …/reschedule). */
export async function rescheduleEvento(
  id: string,
  periodo: { inicio: string; fim: string }
): Promise<void> {
  if (env.useMocks) {
    if (!mockUpdateEvento(id, periodo)) throw notFound()

    await mockResponse(null, 100)
    return
  }

  await apiClient.patch(endpoints.agenda.reschedule(id), {
    startAt: periodo.inicio,
    endAt: periodo.fim,
  })
}

/** Conclui (ou reabre) o compromisso: PATCH …/baixa. */
export async function concluirEvento(id: string, concluido: boolean): Promise<void> {
  if (env.useMocks) {
    if (!mockConcluirEvento(id, concluido)) throw notFound()

    await mockResponse(null, 100)
    return
  }

  await apiClient.patch(endpoints.agenda.baixa(id), { done: concluido })
}

export async function deleteEvento(id: string): Promise<void> {
  if (env.useMocks) {
    if (!mockDeleteEvento(id)) throw notFound()

    await mockResponse(null, 100)
    return
  }

  await apiClient.delete(endpoints.agenda.eventoById(id))
}
