import { ApiError } from '@/lib/api/errors'
import { useAuthStore } from '@/stores/authStore'
import {
  changeStatus,
  getAtendimento,
  getAtendimentos,
  getMensagens,
  linkCliente,
  sendMensagem,
} from './api'
import { mockClientMessage, resetMockAtendimentos } from './mocks'

const ME = 'adv-1'

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
  resetMockAtendimentos()
  useAuthStore.getState().setSession({
    accessToken: 't',
    expiresAt: null,
    user: {
      id: ME,
      email: 'a@b.c',
      name: 'Ana',
      avatarUrl: null,
      role: 'Lawyer',
      tenantId: 'tenant-1',
    },
  })
})

afterEach(() => {
  vi.useRealTimers()
  useAuthStore.getState().clear()
})

const run = async <T>(promise: Promise<T>) => {
  await vi.advanceTimersByTimeAsync(1000)
  return promise
}

async function errorOf(promise: Promise<unknown>) {
  const settled = promise.then(
    () => null,
    (error: unknown) => error
  )
  await vi.advanceTimersByTimeAsync(1000)
  return (await settled) as ApiError
}

describe('fila (modo simulado)', () => {
  it('prioriza quem espera um humano; encerrados vão para o fim', async () => {
    const page = await run(getAtendimentos())

    expect(page.items.map((a) => a.status)).toEqual([
      'PendingHuman',
      'PendingHuman',
      'Human',
      'Bot',
      'Closed',
    ])
    expect(page.totalCount).toBe(5)
  })

  it('filtra por status', async () => {
    const page = await run(getAtendimentos({ status: 'PendingHuman' }))

    expect(page.items).toHaveLength(2)
    expect(page.items.every((a) => a.status === 'PendingHuman')).toBe(true)
  })

  it('busca por nome (sem diferenciar maiúsculas) ou por trecho do telefone', async () => {
    expect(
      (await run(getAtendimentos({ search: 'MARIA' }))).items.map((a) => a.contactName)
    ).toEqual(['Maria Souza'])
    expect((await run(getAtendimentos({ search: '5555' }))).items.map((a) => a.id)).toEqual([
      'atd-004',
      'atd-005',
    ])
  })

  it('traz o trecho da última mensagem', async () => {
    const [first] = (await run(getAtendimentos({ status: 'PendingHuman' }))).items

    expect(first?.lastMessagePreview).toContain('audiência')
  })

  it('"meus atendimentos" não inclui conversas de outro advogado', async () => {
    expect((await run(getAtendimentos({ assignedUserId: ME }))).items).toHaveLength(0)
    expect((await run(getAtendimentos({ assignedUserId: 'outro-advogado' }))).items).toHaveLength(1)
  })
})

describe('conversa (modo simulado)', () => {
  it('devolve as mensagens em ordem cronológica', async () => {
    const page = await run(getMensagens('atd-001'))

    expect(page.items.map((m) => m.sender)).toEqual(['Client', 'Bot', 'Client', 'Bot', 'Client'])
    expect(page.hasMore).toBe(false)
  })

  it('atendimento inexistente é 404 ATENDIMENTO_NOT_FOUND', async () => {
    const error = await errorOf(getAtendimento('nao-existe'))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(404)
    expect(error.code).toBe('ATENDIMENTO_NOT_FOUND')
  })
})

describe('handoff (modo simulado) — as regras do Core', () => {
  it('assumir muda para Humano e o atendimento passa a ser do advogado', async () => {
    await run(changeStatus('atd-001', 'Human'))

    const atendimento = await run(getAtendimento('atd-001'))
    expect(atendimento.status).toBe('Human')
    expect(atendimento.assignedUserId).toBe(ME)
  })

  it('assumir uma conversa que é de outro advogado é conflito', async () => {
    const error = await errorOf(changeStatus('atd-003', 'Human'))

    expect(error.status).toBe(409)
    expect(error.code).toBe('ATENDIMENTO_ALREADY_ASSIGNED')
  })

  it('falar assume a conversa e a mensagem sai enviada', async () => {
    const result = await run(sendMensagem('atd-001', '  Bom dia, Maria.  '))

    expect(result.delivery).toBe('Sent')
    const atendimento = await run(getAtendimento('atd-001'))
    expect(atendimento.status).toBe('Human')
    const { items } = await run(getMensagens('atd-001'))
    expect(items.at(-1)).toMatchObject({
      sender: 'Human',
      body: 'Bom dia, Maria.',
      direction: 'Outbound',
    })
  })

  it('não se fala numa conversa de outro advogado nem numa encerrada', async () => {
    expect((await errorOf(sendMensagem('atd-003', 'oi'))).code).toBe('ATENDIMENTO_ALREADY_ASSIGNED')
    expect((await errorOf(sendMensagem('atd-005', 'oi'))).code).toBe('ATENDIMENTO_CLOSED')
  })

  it('mensagem vazia é recusada', async () => {
    expect((await errorOf(sendMensagem('atd-001', '   '))).code).toBe('ATENDIMENTO_MESSAGE_EMPTY')
  })

  it('devolver ao bot limpa o responsável; devolver quem já está com o bot é transição inválida', async () => {
    await run(changeStatus('atd-001', 'Human'))
    await run(changeStatus('atd-001', 'Bot'))

    const atendimento = await run(getAtendimento('atd-001'))
    expect(atendimento.status).toBe('Bot')
    expect(atendimento.assignedUserId).toBeNull()
    expect((await errorOf(changeStatus('atd-004', 'Bot'))).code).toBe(
      'ATENDIMENTO_INVALID_STATUS_TRANSITION'
    )
  })

  it('encerrado é terminal', async () => {
    await run(changeStatus('atd-001', 'Closed'))

    expect((await errorOf(changeStatus('atd-001', 'Human'))).code).toBe('ATENDIMENTO_CLOSED')
    expect((await errorOf(changeStatus('atd-001', 'Closed'))).code).toBe('ATENDIMENTO_CLOSED')
  })

  it('vincula a conversa a um cliente', async () => {
    await run(linkCliente('atd-004', 'cliente-9'))

    expect((await run(getAtendimento('atd-004'))).clientId).toBe('cliente-9')
  })
})

describe('simulador do cliente e do bot', () => {
  it('"2" com o bot no comando pede o handoff e o bot avisa o cliente', async () => {
    const { events } = mockClientMessage('atd-004', '2')

    expect(events.map((e) => e.status ?? e.sender)).toEqual(['Client', 'PendingHuman', 'Bot'])
    const { items } = await run(getMensagens('atd-004'))
    expect(items.at(-1)?.body).toBe('Aguarde, um advogado vai te atender.')
    expect((await run(getAtendimento('atd-004'))).status).toBe('PendingHuman')
  })

  it('com humano na conversa, o bot fica em silêncio: só a mensagem do cliente é registrada', async () => {
    mockClientMessage('atd-003', '2')

    const { items } = await run(getMensagens('atd-003'))
    expect(items.at(-1)).toMatchObject({ sender: 'Client', body: '2' })
    expect((await run(getAtendimento('atd-003'))).status).toBe('Human')
  })

  it('em Aguardando o bot também fica em silêncio', async () => {
    const before = (await run(getMensagens('atd-001'))).items.length

    mockClientMessage('atd-001', 'alguém aí?')

    expect((await run(getMensagens('atd-001'))).items).toHaveLength(before + 1)
  })
})
