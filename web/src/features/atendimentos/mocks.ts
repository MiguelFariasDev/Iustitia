import { ApiError } from '@/lib/api/errors'
import { minutesAgo } from '@/mocks/dates'
import { useAuthStore } from '@/stores/authStore'
import type { AtendimentoRealtimePayload } from './contract'
import type {
  Atendimento,
  AtendimentoStatus,
  AtendimentosFilters,
  AtendimentosPage,
  Mensagem,
  MensagensPage,
  MensagemSender,
  StatusAlvo,
} from './types'

/**
 * Backend simulado, EM MEMÓRIA, para dev e E2E sem a API .NET nem o Bot (NEXT_PUBLIC_API_MOCKS=true).
 * Reimplementa as regras do Core: só o advogado que assumiu fala numa conversa em Human; falar
 * assume a conversa; encerrado é terminal; outro advogado recebe conflito; a fila prioriza quem
 * espera um humano. Uma recarga volta ao estado inicial.
 *
 * `mockClientMessage` faz o papel do cliente E do Bot (digitar "2" pede o handoff), para dar para
 * ver o fluxo inteiro na tela sem WhatsApp. É simulação — o Bot de verdade é outro produto.
 */

const ME_FALLBACK = 'mock-user'

const meId = () => useAuthStore.getState().user?.id ?? ME_FALLBACK

type StoredAtendimento = Omit<Atendimento, 'lastMessagePreview'>

interface Store {
  atendimentos: StoredAtendimento[]
  mensagens: Record<string, Mensagem[]>
  counter: number
}

function msg(id: string, sender: MensagemSender, body: string, minutes: number): Mensagem {
  return {
    id,
    direction: sender === 'Client' ? 'Inbound' : 'Outbound',
    sender,
    body,
    delivery: 'Sent',
    createdAt: minutesAgo(minutes),
  }
}

function seed(): Store {
  const at = (
    id: string,
    phoneNumber: string,
    contactName: string | null,
    status: AtendimentoStatus,
    lastMinutes: number,
    assignedUserId: string | null = null
  ): StoredAtendimento => ({
    id,
    phoneNumber,
    contactName,
    clientId: null,
    status,
    assignedUserId,
    lastMessageAt: minutesAgo(lastMinutes),
    createdAt: minutesAgo(lastMinutes + 30),
  })

  return {
    counter: 100,
    atendimentos: [
      at('atd-001', '5585999998888', 'Maria Souza', 'PendingHuman', 4),
      at('atd-002', '5585988887777', 'João Pereira', 'PendingHuman', 22),
      at('atd-003', '5585977776666', 'Construtora Alfa', 'Human', 45, 'outro-advogado'),
      at('atd-004', '5585966665555', null, 'Bot', 90),
      at('atd-005', '5585955554444', 'Ana Lima', 'Closed', 1500),
    ],
    mensagens: {
      'atd-001': [
        msg('m-001-1', 'Client', 'Olá', 12),
        msg(
          'm-001-2',
          'Bot',
          'Olá! Você está falando com o atendimento automatizado do escritório. Você pode pedir a qualquer momento para falar com uma pessoa.\n\nComo posso ajudar? Digite:\n*1* - Acompanhar meu processo\n*2* - Falar com um advogado',
          12
        ),
        msg('m-001-3', 'Client', '2', 5),
        msg('m-001-4', 'Bot', 'Aguarde, um advogado vai te atender.', 5),
        msg('m-001-5', 'Client', 'Bom dia, preciso falar sobre a audiência de sexta.', 4),
      ],
      'atd-002': [
        msg('m-002-1', 'Client', 'Quero falar com um advogado', 24),
        msg('m-002-2', 'Bot', 'Aguarde, um advogado vai te atender.', 24),
        msg('m-002-3', 'Client', 'Meu processo está parado há meses.', 22),
      ],
      'atd-003': [
        msg('m-003-1', 'Client', 'Precisamos revisar o contrato de locação.', 60),
        msg('m-003-2', 'Human', 'Boa tarde! Pode me enviar o contrato em PDF?', 50),
        msg('m-003-3', 'Client', 'Envio hoje ainda.', 45),
      ],
      'atd-004': [
        msg('m-004-1', 'Client', 'oi', 91),
        msg('m-004-2', 'Bot', 'Como posso ajudar?', 90),
      ],
      'atd-005': [
        msg('m-005-1', 'Client', 'Obrigada pelo retorno!', 1502),
        msg('m-005-2', 'Human', 'Por nada. Atendimento encerrado.', 1500),
      ],
    },
  }
}

let store = seed()

/** Volta ao estado inicial — usado pelos testes. */
export function resetMockAtendimentos() {
  store = seed()
}

function notFound(): never {
  throw new ApiError(404, {
    errorCode: 'ATENDIMENTO_NOT_FOUND',
    detail: 'Atendimento não encontrado.',
  })
}

function find(id: string): StoredAtendimento {
  return store.atendimentos.find((a) => a.id === id) ?? notFound()
}

function preview(id: string): string | null {
  const list = store.mensagens[id] ?? []
  return list.length > 0 ? (list[list.length - 1]?.body ?? null) : null
}

function view(a: StoredAtendimento): Atendimento {
  return { ...a, lastMessagePreview: preview(a.id) }
}

const statusRank: Record<AtendimentoStatus, number> = {
  PendingHuman: 0,
  Human: 1,
  Bot: 2,
  Closed: 3,
}

export function mockListAtendimentos(filters: AtendimentosFilters): AtendimentosPage {
  const term = filters.search?.trim().toLowerCase() ?? ''
  const digits = term.replace(/\D/g, '')

  const filtered = store.atendimentos
    .filter((a) => !filters.status || a.status === filters.status)
    .filter((a) => !filters.assignedUserId || a.assignedUserId === filters.assignedUserId)
    .filter(
      (a) =>
        !term ||
        (a.contactName?.toLowerCase().includes(term) ?? false) ||
        (digits.length >= 3 && a.phoneNumber.includes(digits))
    )
    .sort(
      (a, b) =>
        statusRank[a.status] - statusRank[b.status] ||
        b.lastMessageAt.localeCompare(a.lastMessageAt)
    )

  const pageSize = filters.pageSize ?? 20
  const page = filters.page ?? 1
  const start = (page - 1) * pageSize

  return {
    items: filtered.slice(start, start + pageSize).map(view),
    page,
    pageSize,
    totalCount: filtered.length,
    totalPages: Math.max(1, Math.ceil(filtered.length / pageSize)),
  }
}

export function mockGetAtendimento(id: string): Atendimento {
  return view(find(id))
}

export function mockListMensagens(id: string, before?: string | null, limit = 50): MensagensPage {
  find(id)
  const all = (store.mensagens[id] ?? []).filter((m) => !before || m.createdAt < before)
  const items = all.slice(Math.max(0, all.length - limit))

  return { items, hasMore: all.length > limit }
}

function append(id: string, sender: MensagemSender, body: string): Mensagem {
  const mensagem: Mensagem = {
    id: `m-${++store.counter}`,
    direction: sender === 'Client' ? 'Inbound' : 'Outbound',
    sender,
    body,
    delivery: sender === 'Human' ? 'Pending' : 'Sent',
    createdAt: new Date().toISOString(),
  }
  const a = find(id)
  ;(store.mensagens[id] ??= []).push(mensagem)
  a.lastMessageAt = mensagem.createdAt

  return mensagem
}

/** O advogado fala: assume a conversa, se ainda não for dele, e a mensagem sai "enviada". */
export function mockSendMensagem(
  id: string,
  body: string
): { messageId: string; delivery: 'Sent' } {
  const a = find(id)
  const text = body.trim()

  if (!text) throw new ApiError(400, { errorCode: 'ATENDIMENTO_MESSAGE_EMPTY' })
  if (a.status === 'Closed') throw new ApiError(409, { errorCode: 'ATENDIMENTO_CLOSED' })
  if (a.status === 'Human' && a.assignedUserId !== meId()) {
    throw new ApiError(409, { errorCode: 'ATENDIMENTO_ALREADY_ASSIGNED' })
  }

  a.status = 'Human'
  a.assignedUserId = meId()
  const mensagem = append(id, 'Human', text)
  mensagem.delivery = 'Sent'

  return { messageId: mensagem.id, delivery: 'Sent' }
}

export function mockChangeStatus(id: string, target: StatusAlvo) {
  const a = find(id)

  if (a.status === 'Closed') throw new ApiError(409, { errorCode: 'ATENDIMENTO_CLOSED' })

  if (target === 'Human') {
    if (a.status === 'Human' && a.assignedUserId !== meId()) {
      throw new ApiError(409, { errorCode: 'ATENDIMENTO_ALREADY_ASSIGNED' })
    }
    a.status = 'Human'
    a.assignedUserId = meId()
  } else if (target === 'Bot') {
    if (a.status === 'Bot') {
      throw new ApiError(409, {
        errorCode: 'ATENDIMENTO_INVALID_STATUS_TRANSITION',
        detail: 'Não é possível mudar o atendimento de Bot para Bot.',
      })
    }
    a.status = 'Bot'
    a.assignedUserId = null
  } else {
    a.status = 'Closed'
    a.assignedUserId = null
  }
}

export function mockLinkCliente(id: string, clienteId: string) {
  find(id).clientId = clienteId
}

/**
 * Simula o cliente escrevendo (e o Bot reagindo). Em `Bot`, digitar "2" pede o handoff e o bot
 * avisa o cliente; qualquer outra coisa recebe o menu. Em `PendingHuman`/`Human` o bot fica em
 * silêncio — só registra a mensagem, como o Bot de verdade.
 */
export function mockClientMessage(
  id: string,
  text: string
): { events: AtendimentoRealtimePayload[] } {
  const a = find(id)
  const events: AtendimentoRealtimePayload[] = []

  const inbound = append(id, 'Client', text)
  events.push({ atendimentoId: id, messageId: inbound.id, direction: 'Inbound', sender: 'Client' })

  if (a.status === 'Bot') {
    if (text.trim() === '2') {
      a.status = 'PendingHuman'
      events.push({ atendimentoId: id, status: 'PendingHuman', assignedUserId: null })
      const ack = append(id, 'Bot', 'Aguarde, um advogado vai te atender.')
      events.push({ atendimentoId: id, messageId: ack.id, direction: 'Outbound', sender: 'Bot' })
    } else {
      const reply = append(
        id,
        'Bot',
        'Como posso ajudar? Digite *1* para processos ou *2* para um advogado.'
      )
      events.push({ atendimentoId: id, messageId: reply.id, direction: 'Outbound', sender: 'Bot' })
    }
  }

  return { events }
}

/** Telefone → id do atendimento aberto (para o simulador de eventos achar a conversa). */
export function mockFindOpenByPhone(phoneNumber: string): string | null {
  return (
    store.atendimentos.find((a) => a.phoneNumber === phoneNumber && a.status !== 'Closed')?.id ??
    null
  )
}
