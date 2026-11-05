import type {
  Atendimento,
  AtendimentoStatus,
  Mensagem,
  MensagemDirection,
  MensagemEntrega,
  MensagemSender,
} from './types'
import { atendimentoStatuses } from './types'

/** Item de GET /atendimentos e /atendimentos/{id} (AtendimentoResponse). */
export interface AtendimentoDto {
  id: string
  phoneNumber: string
  contactName: string | null
  clientId: string | null
  status: string
  assignedUserId: string | null
  lastMessageAt: string
  createdAt: string
  lastMessagePreview: string | null
}

export interface AtendimentosPageDto {
  items: AtendimentoDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

/** Item de GET /atendimentos/{id}/mensagens (MensagemResponse). */
export interface MensagemDto {
  id: string
  direction: string
  sender: string
  body: string
  delivery: string
  createdAt: string
}

export interface MensagensPageDto {
  items: MensagemDto[]
  hasMore: boolean
}

/** POST /atendimentos/{id}/mensagens (SendMessageResponse). */
export interface SendMessageDto {
  messageId: string
  delivery: string
}

/**
 * Payload do Realtime (AtendimentoUpdatedEvent / AtendimentoMessageCreatedEvent). O backend manda
 * SÓ ids e estado — nunca telefone nem texto —, então o conteúdo vem sempre pela API autenticada.
 */
export interface AtendimentoRealtimePayload {
  atendimentoId: string
  status?: string
  assignedUserId?: string | null
  messageId?: string
  direction?: string
  sender?: string
}

/**
 * Status desconhecido vira 'Bot', e isso é deliberado: é o único que mantém o campo de digitação
 * DESABILITADO. Um status novo do backend nunca pode liberar o advogado a escrever numa conversa
 * em estado que a tela não entende.
 */
function toStatus(value: string): AtendimentoStatus {
  return atendimentoStatuses.find((status) => status === value) ?? 'Bot'
}

function toSender(value: string): MensagemSender {
  return value === 'Bot' || value === 'Human' ? value : 'Client'
}

function toDirection(value: string): MensagemDirection {
  return value === 'Outbound' ? 'Outbound' : 'Inbound'
}

function toEntrega(value: string): MensagemEntrega {
  return value === 'Pending' || value === 'Failed' ? value : 'Sent'
}

export function toAtendimento(dto: AtendimentoDto): Atendimento {
  return {
    id: dto.id,
    phoneNumber: dto.phoneNumber,
    contactName: dto.contactName,
    clientId: dto.clientId,
    status: toStatus(dto.status),
    assignedUserId: dto.assignedUserId,
    lastMessageAt: dto.lastMessageAt,
    createdAt: dto.createdAt,
    lastMessagePreview: dto.lastMessagePreview,
  }
}

export function toMensagem(dto: MensagemDto): Mensagem {
  return {
    id: dto.id,
    direction: toDirection(dto.direction),
    sender: toSender(dto.sender),
    body: dto.body,
    delivery: toEntrega(dto.delivery),
    createdAt: dto.createdAt,
  }
}

/** O canal é compartilhado por todo o escritório: payload malformado é descartado, nunca lança. */
export function parseRealtimePayload(payload: unknown): AtendimentoRealtimePayload | null {
  if (typeof payload !== 'object' || payload === null) return null

  const record = payload as Record<string, unknown>
  if (typeof record.atendimentoId !== 'string' || record.atendimentoId.length === 0) return null

  return {
    atendimentoId: record.atendimentoId,
    status: typeof record.status === 'string' ? record.status : undefined,
    assignedUserId: typeof record.assignedUserId === 'string' ? record.assignedUserId : null,
    messageId: typeof record.messageId === 'string' ? record.messageId : undefined,
    direction: typeof record.direction === 'string' ? record.direction : undefined,
    sender: typeof record.sender === 'string' ? record.sender : undefined,
  }
}
