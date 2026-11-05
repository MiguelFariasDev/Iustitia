import type { Route } from 'next'
import {
  Banknote,
  Bell,
  BriefcaseBusiness,
  CalendarDays,
  ChartNoAxesCombined,
  FolderOpen,
  Headset,
  LayoutDashboard,
  type LucideIcon,
  Newspaper,
  PenLine,
  Settings,
  Users,
} from 'lucide-react'
import type { Screen } from '@/lib/auth/access'
import { env } from './env'
import { routes } from './routes'

/** Contadores ao vivo exibidos como badge na navegação (ver `useNavCounts`). */
export type NavBadge = 'publications' | 'alerts'

export interface NavItem {
  /** Tela da matriz de acesso (lib/auth/access.ts): decide se o item aparece. */
  screen: Screen
  label: string
  icon: LucideIcon
  href: Route
  /** Módulo das Fases 2–5: a rota existe, mas a página é um placeholder. */
  soon?: boolean
  /**
   * A tela existe e funciona, mas os dados são de demonstração (ainda não há endpoint no
   * backend). O menu avisa, para ninguém tomar decisão em cima de número inventado.
   */
  demo?: boolean
  /** Contador de pendências exibido à direita do rótulo. */
  badge?: NavBadge
  /** Funcionalidade assistida por IA (recebe o selo ✨). */
  ai?: boolean
}

export interface NavGroup {
  label?: string
  items: NavItem[]
}

export const mainNavigation: NavGroup[] = [
  {
    items: [
      {
        screen: 'dashboard',
        label: 'Área de trabalho',
        icon: LayoutDashboard,
        href: routes.dashboard,
      },
      { screen: 'agenda', label: 'Agenda', icon: CalendarDays, href: routes.agenda },
      {
        screen: 'publications',
        label: 'Publicações',
        icon: Newspaper,
        href: routes.publications,
        badge: 'publications',
      },
      { screen: 'alerts', label: 'Alertas', icon: Bell, href: routes.alerts, badge: 'alerts' },
    ],
  },
  {
    label: 'Gestão',
    items: [
      { screen: 'processes', label: 'Processos', icon: BriefcaseBusiness, href: routes.processes },
      { screen: 'clients', label: 'Clientes', icon: Users, href: routes.clients },
      {
        screen: 'contacts',
        label: 'Contatos',
        icon: Users,
        href: routes.contacts,
        demo: env.useMocks,
      },
      {
        screen: 'serviceDesk',
        label: 'Atendimentos',
        icon: Headset,
        href: routes.serviceDesk,
      },
      { screen: 'finance', label: 'Financeiro', icon: Banknote, href: routes.finance, demo: true },
    ],
  },
  {
    label: 'Ferramentas',
    items: [
      {
        screen: 'drafting',
        label: 'Criação de peças',
        icon: PenLine,
        href: routes.drafting,
        soon: true,
        ai: true,
      },
      {
        screen: 'documents',
        label: 'Documentos',
        icon: FolderOpen,
        href: routes.documents,
        demo: true,
      },
      {
        screen: 'indicators',
        label: 'Indicadores',
        icon: ChartNoAxesCombined,
        href: routes.indicators,
        demo: true,
      },
    ],
  },
]

/** Itens fixos no rodapé da barra lateral. */
export const footerNavigation: NavItem[] = [
  {
    screen: 'settings.profile',
    label: 'Configurações',
    icon: Settings,
    href: routes.settings.profile,
  },
]

export const settingsNavigation: {
  screen: Screen
  label: string
  href: Route
  description: string
}[] = [
  {
    screen: 'settings.profile',
    label: 'Perfil',
    href: routes.settings.profile,
    description: 'Seus dados e credenciais',
  },
  {
    screen: 'settings.tenant',
    label: 'Escritório',
    href: routes.settings.tenant,
    description: 'Dados cadastrais do escritório',
  },
  {
    screen: 'settings.users',
    label: 'Usuários',
    href: routes.settings.users,
    description: 'Equipe, papéis e convites',
  },
  {
    screen: 'settings.notifications',
    label: 'Notificações',
    href: routes.settings.notifications,
    description: 'Canais e preferências de alerta',
  },
  {
    screen: 'settings.integrations',
    label: 'Integrações',
    href: routes.settings.integrations,
    description: 'Tribunais, IA e calendários',
  },
  {
    screen: 'settings.audit',
    label: 'Auditoria',
    href: routes.settings.audit,
    description: 'Registro imutável de ações',
  },
]

/** Rótulos dos segmentos de URL para breadcrumbs. */
export const segmentLabels: Record<string, string> = {
  painel: 'Área de trabalho',
  publicacoes: 'Publicações',
  alertas: 'Alertas',
  agenda: 'Agenda',
  processos: 'Processos',
  clientes: 'Clientes',
  novo: 'Novo',
  editar: 'Editar',
  contatos: 'Contatos',
  atendimentos: 'Atendimentos',
  financeiro: 'Financeiro',
  'criacao-de-pecas': 'Criação de peças',
  documentos: 'Documentos',
  indicadores: 'Indicadores',
  configuracoes: 'Configurações',
  perfil: 'Perfil',
  escritorio: 'Escritório',
  usuarios: 'Usuários',
  notificacoes: 'Notificações',
  integracoes: 'Integrações',
  auditoria: 'Auditoria',
}

export function isActivePath(pathname: string, href: string) {
  if (href === routes.settings.profile) return pathname.startsWith(routes.settings.root)
  return pathname === href || pathname.startsWith(`${href}/`)
}

/**
 * Aplica a matriz de acesso a uma navegação: tira o que o usuário não alcança e os grupos que
 * ficarem vazios. `label` troca o rótulo de itens que mudam por tipo de conta.
 */
export function filterNavigation(
  groups: NavGroup[],
  can: (screen: Screen) => boolean,
  label: (screen: Screen, fallback: string) => string
): NavGroup[] {
  return groups
    .map((group) => ({
      ...group,
      items: group.items
        .filter((item) => can(item.screen))
        .map((item) => ({ ...item, label: label(item.screen, item.label) })),
    }))
    .filter((group) => group.items.length > 0)
}
