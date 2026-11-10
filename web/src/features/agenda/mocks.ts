import { mockUser } from '@/lib/auth/mockAuth'
import { daysFromNow } from '@/mocks/dates'
import type { AgendaEvento, AgendaPeriodo } from './types'

/**
 * Repositório em memória da agenda, usado com NEXT_PUBLIC_API_MOCKS ligado. Datas relativas a
 * "agora" para a tela nunca ficar vazia ou desatualizada.
 */
function seed(): AgendaEvento[] {
  const evento = (
    n: number,
    titulo: string,
    tipo: AgendaEvento['tipo'],
    dias: number,
    hora: number,
    horas = 1,
    responsavelId: string | null = mockUser.id
  ): AgendaEvento => ({
    id: `00000000-0000-4000-8000-00000000000${n}`,
    titulo,
    descricao: null,
    tipo,
    inicio: daysFromNow(dias, hora),
    fim: daysFromNow(dias, hora + horas),
    diaInteiro: false,
    local: null,
    processoId: null,
    responsavelId,
  })

  return [
    evento(1, 'Audiência de instrução', 'Audiencia', 1, 14, 2),
    evento(2, 'Prazo: contestação', 'Prazo', 2, 9),
    evento(3, 'Reunião com cliente', 'Reuniao', 0, 16),
    evento(4, 'Revisar minuta de acordo', 'Tarefa', 3, 11, 1, 'u-2'),
    evento(5, 'Prazo: embargos de declaração', 'Prazo', 5, 9, 1, 'u-2'),
  ]
}

let eventos = seed()

export function resetAgendaMock() {
  eventos = seed()
}

export function mockListEventos(
  { from, to }: AgendaPeriodo,
  responsavelId: string | null = null
): AgendaEvento[] {
  return eventos
    .filter((e) => e.inicio < to && e.fim >= from)
    .filter((e) => responsavelId === null || e.responsavelId === responsavelId)
    .sort((a, b) => a.inicio.localeCompare(b.inicio))
}

export function mockCreateEvento(
  values: Omit<AgendaEvento, 'id' | 'fim'> & { fim: string | null }
): AgendaEvento {
  const inicio = new Date(values.inicio)
  const fim =
    values.fim ??
    new Date(inicio.getTime() + (values.diaInteiro ? 86_400_000 : 3_600_000)).toISOString()
  const criado: AgendaEvento = {
    ...values,
    fim,
    id: crypto.randomUUID(),
    responsavelId: values.responsavelId ?? mockUser.id,
  }
  eventos = [...eventos, criado]

  return criado
}

export function mockUpdateEvento(id: string, patch: Partial<AgendaEvento>): AgendaEvento | null {
  const atual = eventos.find((e) => e.id === id)
  if (!atual) return null

  const atualizado = { ...atual, ...patch, id }
  eventos = eventos.map((e) => (e.id === id ? atualizado : e))

  return atualizado
}

export function mockDeleteEvento(id: string): boolean {
  const antes = eventos.length
  eventos = eventos.filter((e) => e.id !== id)

  return eventos.length < antes
}
