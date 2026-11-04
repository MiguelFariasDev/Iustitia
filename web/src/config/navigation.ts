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
import { routes } from './routes'

/** Contadores ao vivo exibidos como badge na navegação (ver `useNavCounts`). */
export type NavBadge = 'publications' | 'alerts'

export interface NavItem {
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
      { label: 'Área de trabalho', icon: LayoutDashboard, href: routes.dashboard },
      { label: 'Agenda', icon: CalendarDays, href: routes.agenda, soon: true },
      { label: 'Publicações', icon: Newspaper, href: routes.publications, badge: 'publications' },
      { label: 'Alertas', icon: Bell, href: routes.alerts, badge: 'alerts' },
    ],
  },
  {
    label: 'Gestão',
    items: [
      { label: 'Processos', icon: BriefcaseBusiness, href: routes.processes },
      { label: 'Clientes', icon: Users, href: routes.clients },
      { label: 'Contatos', icon: Users, href: routes.contacts, demo: true },
      { label: 'Atendimentos', icon: Headset, href: routes.serviceDesk, soon: true },
      { label: 'Financeiro', icon: Banknote, href: routes.finance, demo: true },
    ],
  },
  {
    label: 'Ferramentas',
    items: [
      { label: 'Criação de peças', icon: PenLine, href: routes.drafting, soon: true, ai: true },
      { label: 'Documentos', icon: FolderOpen, href: routes.documents, demo: true },
      { label: 'Indicadores', icon: ChartNoAxesCombined, href: routes.indicators, demo: true },
    ],
  },
]

/** Itens fixos no rodapé da barra lateral. */
export const footerNavigation: NavItem[] = [
  { label: 'Configurações', icon: Settings, href: routes.settings.profile },
]

export const settingsNavigation: { label: string; href: Route; description: string }[] = [
  { label: 'Perfil', href: routes.settings.profile, description: 'Seus dados e credenciais' },
  {
    label: 'Escritório',
    href: routes.settings.tenant,
    description: 'Dados cadastrais do escritório',
  },
  { label: 'Usuários', href: routes.settings.users, description: 'Equipe, papéis e convites' },
  {
    label: 'Notificações',
    href: routes.settings.notifications,
    description: 'Canais e preferências de alerta',
  },
  {
    label: 'Integrações',
    href: routes.settings.integrations,
    description: 'Tribunais, IA e calendários',
  },
  { label: 'Auditoria', href: routes.settings.audit, description: 'Registro imutável de ações' },
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
