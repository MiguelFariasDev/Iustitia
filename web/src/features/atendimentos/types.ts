/** Espelha Atendimentos.Domain.Atendimentos.AtendimentoStatus. */
export type AtendimentoStatus = 'Bot' | 'PendingHuman' | 'Human' | 'Closed'

export const atendimentoStatuses: AtendimentoStatus[] = ['Bot', 'PendingHuman', 'Human', 'Closed']

/** Espelha MessageSender: quem escreveu a mensagem. */
export type MensagemSender = 'Client' | 'Bot' | 'Human'

/** Espelha MessageDirection. */
export type MensagemDirection = 'Inbound' | 'Outbound'

/** Espelha DeliveryStatus: situação do envio ao WhatsApp (só faz sentido para o que sai). */
export type MensagemEntrega = 'Pending' | 'Sent' | 'Failed'

export interface Atendimento {
  id: string
  /** Só dígitos, com DDI (formato wa_id da Meta). Use `formatPhone` para exibir. */
  phoneNumber: string
  /** Nome do perfil do WhatsApp, informado pela Meta. Pode faltar. */
  contactName: string | null
  /** Cliente do escritório vinculado a este número, quando houver. */
  clientId: string | null
  status: AtendimentoStatus
  /** Advogado que assumiu — preenchido só enquanto status = Human. */
  assignedUserId: string | null
  lastMessageAt: string
  createdAt: string
  /** Trecho da última mensagem, para a lista. */
  lastMessagePreview: string | null
}

export interface Mensagem {
  id: string
  direction: MensagemDirection
  sender: MensagemSender
  body: string
  delivery: MensagemEntrega
  createdAt: string
}

/** Abas da fila: cada uma é um PRESET de status (ver tabFilters). */
export type AtendimentoTab = 'all' | 'pending' | 'human' | 'bot' | 'closed'

export const atendimentoTabs: AtendimentoTab[] = ['all', 'pending', 'human', 'bot', 'closed']

export interface AtendimentosFilters {
  page?: number
  pageSize?: number
  status?: AtendimentoStatus | null
  /** Só os atendimentos que o próprio advogado assumiu. */
  assignedUserId?: string | null
  /** Nome do contato ou trecho do telefone. */
  search?: string
}

export interface AtendimentosPage {
  items: Atendimento[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface MensagensPage {
  /** Em ordem cronológica (a mais antiga primeiro). */
  items: Mensagem[]
  /** Há mensagens anteriores às carregadas. */
  hasMore: boolean
}

export type StatusAlvo = Extract<AtendimentoStatus, 'Human' | 'Bot' | 'Closed'>
