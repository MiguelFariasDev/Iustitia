import {
  Briefcase,
  CalendarClock,
  CalendarPlus,
  CheckSquare,
  FilePlus2,
  FileText,
  Gavel,
  type LucideIcon,
  UserPlus,
  Users,
} from 'lucide-react'
import type { Route } from 'next'
import { routes } from '@/config/routes'

export interface CreateAction {
  id: string
  label: string
  icon: LucideIcon
  /** Ausente = módulo ainda não disponível nesta fase. */
  href?: Route
  shortcutHint?: string
}

export const createActions: CreateAction[] = [
  { id: 'process', label: 'Novo processo', icon: Briefcase, href: '/processos/novo' as Route },
  { id: 'client', label: 'Novo cliente', icon: Users, href: '/clientes/novo' as Route },
  { id: 'task', label: 'Nova tarefa', icon: CheckSquare, href: `${routes.tasks}?nova=1` as Route },
  // Prazo, audiência e evento abrem o formulário da agenda já no tipo escolhido (?novo=prazo|audiencia|evento).
  { id: 'deadline', label: 'Novo prazo', icon: CalendarClock, href: `${routes.agenda}?novo=prazo` as Route },
  { id: 'hearing', label: 'Nova audiência', icon: Gavel, href: `${routes.agenda}?novo=audiencia` as Route },
  { id: 'event', label: 'Novo evento', icon: CalendarPlus, href: `${routes.agenda}?novo=evento` as Route },
  { id: 'document', label: 'Novo documento', icon: FilePlus2 },
  { id: 'brief', label: 'Nova peça', icon: FileText, href: routes.drafting },
  {
    id: 'user',
    label: 'Novo usuário',
    icon: UserPlus,
    href: `${routes.settings.users}?convidar=1` as Route,
  },
]

/** Opções do "Novo" quando a tela aberta é um processo: tudo nasce ligado a ele. */
export const opcoesNoProcesso = [
  { id: 'tarefa', label: 'Tarefa' },
  { id: 'evento', label: 'Evento' },
  { id: 'prazo', label: 'Prazo' },
  { id: 'audiencia', label: 'Audiência' },
  { id: 'atendimento', label: 'Atendimento' },
  { id: 'documento', label: 'Documento' },
] as const

export type OpcaoNoProcesso = (typeof opcoesNoProcesso)[number]['id']

const UUID = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
const rotaDoProcesso = new RegExp(`^${routes.processes}/(${UUID})$`, 'i')

/** Id do processo se `pathname` é a tela de um processo; senão `null`. */
export function processoDaRota(pathname: string | null): string | null {
  return pathname?.match(rotaDoProcesso)?.[1] ?? null
}

/** Link que abre a tela do processo já com o diálogo de criação aberto (`?novo=prazo`). */
export const hrefNoProcesso = (processoId: string, opcao: OpcaoNoProcesso) =>
  `${routes.processes}/${processoId}?novo=${opcao}` as Route
