import {
  Briefcase,
  CalendarPlus,
  CheckSquare,
  FilePlus2,
  FileText,
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
  { id: 'event', label: 'Novo evento', icon: CalendarPlus, href: `${routes.agenda}?novo=1` as Route },
  { id: 'document', label: 'Novo documento', icon: FilePlus2 },
  { id: 'brief', label: 'Nova peça', icon: FileText, href: routes.drafting },
  {
    id: 'user',
    label: 'Novo usuário',
    icon: UserPlus,
    href: `${routes.settings.users}?convidar=1` as Route,
  },
]
