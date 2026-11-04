import { mockUser } from '@/lib/auth/mockAuth'
import { minutesAgo } from '@/mocks/dates'
import type {
  AuditEntry,
  Integration,
  NotificationPreference,
  Profile,
  TeamUser,
  TenantProfile,
} from './types'

export const profileMock: Profile = {
  name: mockUser.name,
  email: mockUser.email,
  phone: '51999998888',
  oabNumber: '123456',
  oabState: 'RS',
}

export const tenantProfileMock: TenantProfile = {
  name: 'Souza & Associados Advocacia',
  cnpj: '11.222.333/0001-81',
  oabState: 'RS',
  email: 'contato@souzaassociados.adv.br',
  phone: '5133334444',
  address: 'Av. Borges de Medeiros, 1000 — sala 501',
  city: 'Porto Alegre',
  timezone: 'America/Sao_Paulo',
}

export const teamUsersMock: TeamUser[] = [
  {
    id: mockUser.id,
    name: 'Ana Souza',
    email: mockUser.email,
    role: 'Owner',
    status: 'active',
    lastSignInAt: minutesAgo(5),
  },
  {
    id: 'u-2',
    name: 'Bruno Lima',
    email: 'bruno.lima@escritorio.com.br',
    role: 'Lawyer',
    status: 'active',
    lastSignInAt: minutesAgo(60 * 3),
  },
  {
    id: 'u-3',
    name: 'Carolina Reis',
    email: 'carolina.reis@escritorio.com.br',
    role: 'Partner',
    status: 'active',
    lastSignInAt: minutesAgo(60 * 30),
  },
  {
    id: 'u-4',
    name: 'Diego Martins',
    email: 'diego.martins@escritorio.com.br',
    role: 'Intern',
    status: 'invited',
    lastSignInAt: null,
  },
  {
    id: 'u-5',
    name: 'Elisa Castro',
    email: 'elisa.castro@escritorio.com.br',
    role: 'Financial',
    status: 'active',
    lastSignInAt: minutesAgo(60 * 50),
  },
  {
    id: 'u-6',
    name: 'Fábio Nunes',
    email: 'fabio.nunes@escritorio.com.br',
    role: 'Admin',
    status: 'suspended',
    lastSignInAt: minutesAgo(60 * 24 * 40),
  },
]

export const notificationPreferencesMock: NotificationPreference[] = [
  {
    key: 'deadline.approaching',
    label: 'Prazos próximos do vencimento',
    description: 'Lembretes 5, 2 e 1 dia(s) antes e no dia do prazo.',
    channels: { email: true, push: true, inApp: true },
    locked: true,
  },
  {
    key: 'publication.captured',
    label: 'Novas publicações',
    description: 'Quando publicações forem capturadas dos diários oficiais.',
    channels: { email: true, push: false, inApp: true },
  },
  {
    key: 'review.pending',
    label: 'Sugestões de IA aguardando revisão',
    description: 'Classificações e prazos sugeridos que precisam da sua confirmação.',
    channels: { email: false, push: true, inApp: true },
  },
  {
    key: 'task.assigned',
    label: 'Tarefas atribuídas a mim',
    description: 'Quando alguém da equipe atribuir uma tarefa a você.',
    channels: { email: true, push: true, inApp: true },
  },
  {
    key: 'team.changes',
    label: 'Mudanças na equipe',
    description: 'Convites aceitos, papéis alterados e acessos removidos.',
    channels: { email: false, push: false, inApp: true },
  },
]

export const integrationsMock: Integration[] = [
  {
    id: 'supabase',
    name: 'Supabase Auth',
    description: 'Autenticação, MFA e sessões seguras.',
    status: 'connected',
    category: 'Produtividade',
  },
  {
    id: 'cnj',
    name: 'CNJ / DataJud',
    description: 'Captura automática de publicações e andamentos processuais.',
    status: 'soon',
    category: 'Tribunais',
  },
  {
    id: 'anthropic',
    name: 'Anthropic Claude',
    description: 'Classificação de publicações e assistente Athena — sempre com revisão humana.',
    status: 'soon',
    category: 'Inteligência artificial',
  },
  {
    id: 'google-calendar',
    name: 'Google Agenda',
    description: 'Sincronize audiências e prazos com sua agenda.',
    status: 'soon',
    category: 'Produtividade',
  },
  {
    id: 'whatsapp',
    name: 'WhatsApp',
    description: 'Notificações de prazos e atualizações para a equipe.',
    status: 'soon',
    category: 'Comunicação',
  },
]

const actions = [
  'Login realizado',
  'Usuário convidado',
  'Papel alterado',
  'Configurações atualizadas',
  'Feature flag alterada',
  'Logout',
]
const actors = ['Ana Souza', 'Bruno Lima', 'Carolina Reis', 'Sistema']
const entities = ['Sessão', 'Usuário', 'Escritório', 'Feature flag']

export const auditEntriesMock: AuditEntry[] = Array.from({ length: 37 }, (_, i) => ({
  id: `audit-${i + 1}`,
  occurredAt: minutesAgo(i * 97 + 3),
  actor: actors[i % actors.length] ?? 'Sistema',
  action: actions[i % actions.length] ?? 'Ação',
  entity: entities[i % entities.length] ?? 'Recurso',
  ipAddress: `177.54.${(i * 7) % 255}.${(i * 13) % 255}`,
}))
