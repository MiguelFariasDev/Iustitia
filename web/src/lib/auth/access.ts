import { routes } from '@/config/routes'
import type { AccountType, UserRole } from './types'

/**
 * Matriz de acesso às telas: tipo de conta × papel.
 *
 * Isto é conveniência de interface, NÃO segurança. Esconder uma tela evita mostrar ao usuário
 * um menu que só responderia 403; quem barra de verdade são as policies da API (ADR-028) e o
 * RLS (ADR-004). Por isso a matriz espelha as policies reais:
 *
 *   LawyerOrAbove  = Owner, Partner, Lawyer   (processos, clientes, publicações, alertas…)
 *   PartnerOrAbove = Owner, Partner           (auditoria, convidar usuários, configurações)
 *
 * Mudou uma policy no backend? Mude a linha correspondente aqui — e o teste de `access.test.ts`
 * que lista cada tela por papel é o que acusa a divergência na revisão.
 *
 * Conta Solo: uma pessoa só, sempre Owner. Some o que só faz sentido com equipe (usuários,
 * filtro por responsável) e o cadastro do "escritório" vira "dados profissionais".
 */
export type Screen =
  | 'dashboard'
  | 'agenda'
  | 'publications'
  | 'review'
  | 'alerts'
  | 'processes'
  | 'clients'
  | 'contacts'
  | 'serviceDesk'
  | 'finance'
  | 'drafting'
  | 'documents'
  | 'indicators'
  | 'settings.profile'
  | 'settings.tenant'
  | 'settings.users'
  | 'settings.notifications'
  | 'settings.integrations'
  | 'settings.audit'

const allRoles: readonly UserRole[] = ['Owner', 'Partner', 'Lawyer', 'Intern', 'Admin', 'Financial']
const lawyerOrAbove: readonly UserRole[] = ['Owner', 'Partner', 'Lawyer']
const partnerOrAbove: readonly UserRole[] = ['Owner', 'Partner']

interface Rule {
  roles: readonly UserRole[]
  /** Só existe em conta com equipe (Firm). */
  firmOnly?: boolean
}

export const screenRules: Record<Screen, Rule> = {
  dashboard: { roles: allRoles },
  agenda: { roles: allRoles },
  documents: { roles: allRoles },
  publications: { roles: lawyerOrAbove },
  review: { roles: lawyerOrAbove },
  alerts: { roles: lawyerOrAbove },
  processes: { roles: lawyerOrAbove },
  clients: { roles: lawyerOrAbove },
  contacts: { roles: lawyerOrAbove },
  serviceDesk: { roles: lawyerOrAbove },
  drafting: { roles: lawyerOrAbove },
  finance: { roles: ['Owner', 'Partner', 'Financial'] },
  indicators: { roles: partnerOrAbove },
  'settings.profile': { roles: allRoles },
  'settings.notifications': { roles: allRoles },
  'settings.integrations': { roles: allRoles },
  'settings.tenant': { roles: partnerOrAbove },
  'settings.audit': { roles: partnerOrAbove },
  'settings.users': { roles: partnerOrAbove, firmOnly: true },
}

export interface AccessContext {
  role: UserRole | null
  accountType: AccountType | null
}

export function isSolo(context: AccessContext): boolean {
  return context.accountType === 'Solo'
}

/**
 * Papel desconhecido (claim ausente) só enxerga o que é de todos: negar por padrão é o lado
 * seguro quando não sabemos quem é. Tipo de conta desconhecido é tratado como Firm, que é o
 * que sempre existiu antes do ADR-068.
 */
export function canAccess(screen: Screen, context: AccessContext): boolean {
  const rule = screenRules[screen]
  if (rule.firmOnly && isSolo(context)) return false
  if (context.role === null) return rule.roles === allRoles

  return rule.roles.includes(context.role)
}

/** Papel e tipo de conta que dão acesso a cada filtro/recurso que depende de equipe. */
export function hasTeam(context: AccessContext): boolean {
  return !isSolo(context)
}

/** A tela de dados do escritório vira "Dados profissionais" para quem trabalha sozinho. */
export function screenLabel(screen: Screen, fallback: string, context: AccessContext): string {
  if (screen === 'settings.tenant' && isSolo(context)) return 'Dados profissionais'

  return fallback
}

const pathScreens: readonly (readonly [string, Screen])[] = [
  // Mais específica que /publicacoes, então vem antes.
  ['/publicacoes/revisao', 'review'],
  [routes.settings.profile, 'settings.profile'],
  [routes.settings.tenant, 'settings.tenant'],
  [routes.settings.users, 'settings.users'],
  [routes.settings.notifications, 'settings.notifications'],
  [routes.settings.integrations, 'settings.integrations'],
  [routes.settings.audit, 'settings.audit'],
  [routes.dashboard, 'dashboard'],
  [routes.agenda, 'agenda'],
  [routes.publications, 'publications'],
  [routes.alerts, 'alerts'],
  [routes.processes, 'processes'],
  [routes.clients, 'clients'],
  [routes.contacts, 'contacts'],
  [routes.serviceDesk, 'serviceDesk'],
  [routes.finance, 'finance'],
  [routes.drafting, 'drafting'],
  [routes.documents, 'documents'],
  [routes.indicators, 'indicators'],
]

/** Tela a que um caminho pertence; `null` para rotas fora da matriz (ex.: `/configuracoes`). */
export function screenForPath(pathname: string): Screen | null {
  const found = pathScreens.find(([path]) => pathname === path || pathname.startsWith(`${path}/`))

  return found ? found[1] : null
}
