import { toAtendimento, toMensagem, parseRealtimePayload } from './contract'

const dto = {
  id: 'a1',
  phoneNumber: '5585999998888',
  contactName: 'Maria',
  clientId: null,
  status: 'PendingHuman',
  assignedUserId: null,
  lastMessageAt: '2026-11-05T10:00:00Z',
  createdAt: '2026-11-05T09:00:00Z',
  lastMessagePreview: 'Olá',
}

describe('toAtendimento', () => {
  it('mapeia o DTO do backend', () => {
    expect(toAtendimento(dto)).toMatchObject({
      id: 'a1',
      status: 'PendingHuman',
      contactName: 'Maria',
      lastMessagePreview: 'Olá',
    })
  })

  it('status desconhecido vira "Bot" — o único que mantém o campo de digitação bloqueado', () => {
    expect(toAtendimento({ ...dto, status: 'EmAnalise' }).status).toBe('Bot')
    expect(toAtendimento({ ...dto, status: '' }).status).toBe('Bot')
  })
})

describe('toMensagem', () => {
  const base = {
    id: 'm1',
    direction: 'Outbound',
    sender: 'Human',
    body: 'Bom dia',
    delivery: 'Failed',
    createdAt: '2026-11-05T10:00:00Z',
  }

  it('preserva remetente, direção e a falha de entrega', () => {
    expect(toMensagem(base)).toMatchObject({
      sender: 'Human',
      direction: 'Outbound',
      delivery: 'Failed',
    })
  })

  it('valores desconhecidos caem em defaults seguros, sem lançar', () => {
    expect(
      toMensagem({ ...base, sender: 'Alien', direction: 'Sideways', delivery: '?' })
    ).toMatchObject({
      sender: 'Client',
      direction: 'Inbound',
      delivery: 'Sent',
    })
  })
})

describe('parseRealtimePayload', () => {
  it('aceita o aviso de status', () => {
    expect(
      parseRealtimePayload({ atendimentoId: 'a1', status: 'Human', assignedUserId: 'u1' })
    ).toEqual({
      atendimentoId: 'a1',
      status: 'Human',
      assignedUserId: 'u1',
      messageId: undefined,
      direction: undefined,
      sender: undefined,
    })
  })

  it('aceita o aviso de mensagem', () => {
    expect(
      parseRealtimePayload({
        atendimentoId: 'a1',
        messageId: 'm1',
        direction: 'Inbound',
        sender: 'Client',
      })
    ).toMatchObject({ atendimentoId: 'a1', messageId: 'm1', sender: 'Client' })
  })

  it.each([null, undefined, 'texto', 42, [], {}, { atendimentoId: '' }, { atendimentoId: 7 }])(
    'descarta payload malformado (%j) sem lançar',
    (payload) => {
      expect(parseRealtimePayload(payload)).toBeNull()
    }
  )

  it('o canal nunca carrega texto: o parser não expõe corpo de mensagem nem telefone', () => {
    const parsed = parseRealtimePayload({
      atendimentoId: 'a1',
      body: 'segredo de justiça',
      phoneNumber: '5585999998888',
    })

    expect(JSON.stringify(parsed)).not.toContain('segredo')
    expect(JSON.stringify(parsed)).not.toContain('5585999998888')
  })
})
