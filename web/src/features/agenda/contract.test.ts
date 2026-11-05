import { toAgendaEvento, toEventPayload, type CalendarEventDto } from './contract'

const dto: CalendarEventDto = {
  id: 'e1',
  title: 'Prazo: contestação',
  description: null,
  type: 'Prazo',
  startAt: '2026-11-10T12:00:00Z',
  endAt: '2026-11-10T13:00:00Z',
  allDay: false,
  location: null,
  processId: 'p1',
  assignedUserId: 'u1',
}

describe('contrato da agenda', () => {
  it('traduz o DTO do backend para o modelo da tela', () => {
    expect(toAgendaEvento(dto)).toMatchObject({
      titulo: 'Prazo: contestação',
      tipo: 'Prazo',
      inicio: dto.startAt,
      processoId: 'p1',
      responsavelId: 'u1',
    })
  })

  it('tipo desconhecido cai em Tarefa em vez de quebrar a agenda', () => {
    expect(toAgendaEvento({ ...dto, type: 'Novo' }).tipo).toBe('Tarefa')
  })

  it('o payload usa os nomes do backend e troca texto vazio por null', () => {
    const payload = toEventPayload({
      titulo: ' Reunião ',
      tipo: 'Reuniao',
      inicio: dto.startAt,
      fim: null,
      diaInteiro: false,
      descricao: '  ',
      local: 'Sala 2',
      processoId: null,
      responsavelId: 'u-2',
    })

    expect(payload).toEqual({
      title: 'Reunião',
      type: 'Reuniao',
      startAt: dto.startAt,
      endAt: null,
      allDay: false,
      description: null,
      location: 'Sala 2',
      processId: null,
      assignedUserId: 'u-2',
    })
  })
})
