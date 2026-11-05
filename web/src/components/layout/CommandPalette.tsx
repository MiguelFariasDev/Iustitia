'use client'

import { useQuery } from '@tanstack/react-query'
import {
  Briefcase,
  CheckSquare,
  FileText,
  LayoutDashboard,
  LogOut,
  type LucideIcon,
  Newspaper,
  Settings,
  SunMoon,
  Users,
} from 'lucide-react'
import { useRouter } from 'next/navigation'
import { useState } from 'react'
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command'
import { settingsNavigation } from '@/config/navigation'
import { routes } from '@/config/routes'
import { searchGlobal } from '@/features/search/api'
import { matchesSearch } from '@/features/search/normalize'
import {
  searchGroupLabels,
  type SearchResult,
  type SearchResultKind,
} from '@/features/search/types'
import { useAccess } from '@/hooks/useAccess'
import { useAuth } from '@/hooks/useAuth'
import { useDebounce } from '@/hooks/useDebounce'
import { useTheme } from '@/hooks/useTheme'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useUiStore } from '@/stores/uiStore'
import { createActions } from './createActions'
import { useRunCreateAction } from './CreateButton'

const MIN_TERM = 2

const kindIcon: Record<SearchResultKind, LucideIcon> = {
  process: Briefcase,
  client: Users,
  task: CheckSquare,
  publication: Newspaper,
  document: FileText,
}

const kindOrder: SearchResultKind[] = ['process', 'client', 'task', 'publication', 'document']

function groupResults(results: SearchResult[]) {
  const groups = new Map<SearchResultKind, SearchResult[]>()
  for (const result of results) {
    const list = groups.get(result.kind) ?? []
    list.push(result)
    groups.set(result.kind, list)
  }
  return kindOrder.flatMap((kind) => {
    const items = groups.get(kind)
    return items ? [{ kind, items }] : []
  })
}

/** Busca global (Ctrl/⌘+K). Carregada via next/dynamic apenas na primeira abertura. */
export default function CommandPalette() {
  const open = useUiStore((s) => s.commandPaletteOpen)
  const setOpen = useUiStore((s) => s.setCommandPaletteOpen)
  const router = useRouter()
  const { toggleTheme } = useTheme()
  const { signOut } = useAuth()
  const access = useAccess()
  const runCreateAction = useRunCreateAction()
  const [term, setTerm] = useState('')
  const debouncedTerm = useDebounce(term.trim(), 250)
  const searchEnabled = debouncedTerm.length >= MIN_TERM

  const { data: results = [], isFetching } = useQuery({
    queryKey: queryKeys.search.global(debouncedTerm),
    queryFn: () => searchGlobal(debouncedTerm),
    enabled: open && searchEnabled,
    staleTime: 30_000,
  })

  function handleOpenChange(next: boolean) {
    setOpen(next)
    if (!next) setTerm('')
  }

  function run(action: () => void) {
    handleOpenChange(false)
    action()
  }

  const quickActions = [
    ...createActions.map((a) => ({
      id: `create-${a.id}`,
      label: a.label,
      icon: a.icon,
      shortcut: undefined as string | undefined,
      perform: () => runCreateAction(a),
    })),
    {
      id: 'theme',
      label: 'Alternar tema claro/escuro',
      icon: SunMoon,
      shortcut: 'Ctrl ⇧ L',
      perform: toggleTheme,
    },
    {
      id: 'signout',
      label: 'Sair',
      icon: LogOut,
      shortcut: undefined,
      perform: () => void signOut(),
    },
  ].filter((a) => matchesSearch(a.label, term))

  const pages = [
    { label: 'Painel de gestão', href: routes.dashboard, icon: LayoutDashboard },
    ...settingsNavigation
      .filter((s) => !access.isReady || access.can(s.screen))
      .map((s) => ({
        label: `Configurações › ${access.label(s.screen, s.label)}`,
        href: s.href,
        icon: Settings,
      })),
  ].filter((p) => matchesSearch(p.label, term))

  const grouped = searchEnabled ? groupResults(results) : []
  const hasAnything = quickActions.length > 0 || pages.length > 0 || grouped.length > 0

  return (
    <CommandDialog
      open={open}
      onOpenChange={handleOpenChange}
      title="Busca global"
      description="Busque processos, clientes, tarefas, publicações, documentos, páginas e ações."
      shouldFilter={false}
      className="sm:max-w-xl"
    >
      <CommandInput
        value={term}
        onValueChange={setTerm}
        placeholder="Buscar processos, clientes, páginas, ações…"
        aria-label="Termo de busca"
      />
      <CommandList>
        {!hasAnything && !isFetching ? (
          <CommandEmpty>Nenhum resultado para “{term}”.</CommandEmpty>
        ) : null}
        {searchEnabled && isFetching && grouped.length === 0 ? (
          <div role="status" className="px-4 py-3 text-sm text-muted-foreground">
            Buscando…
          </div>
        ) : null}

        {grouped.map(({ kind, items }) => {
          const Icon = kindIcon[kind]
          return (
            <CommandGroup key={kind} heading={searchGroupLabels[kind]}>
              {items.map((item) => (
                <CommandItem
                  key={item.id}
                  value={`${kind}-${item.id}`}
                  onSelect={() =>
                    run(() =>
                      notify.info(`${searchGroupLabels[kind]}: módulo disponível em breve`, {
                        description: item.title,
                      })
                    )
                  }
                >
                  <Icon aria-hidden />
                  <div className="min-w-0">
                    <p className="truncate">{item.title}</p>
                    <p className="truncate text-xs text-muted-foreground">{item.subtitle}</p>
                  </div>
                </CommandItem>
              ))}
            </CommandGroup>
          )
        })}

        {grouped.length > 0 && (pages.length > 0 || quickActions.length > 0) ? (
          <CommandSeparator />
        ) : null}

        {pages.length > 0 ? (
          <CommandGroup heading="Páginas e configurações">
            {pages.map((page) => (
              <CommandItem
                key={page.href}
                value={`page-${page.href}`}
                onSelect={() => run(() => router.push(page.href))}
              >
                <page.icon aria-hidden />
                {page.label}
              </CommandItem>
            ))}
          </CommandGroup>
        ) : null}

        {quickActions.length > 0 ? (
          <CommandGroup heading="Ações rápidas">
            {quickActions.map((action) => (
              <CommandItem key={action.id} value={action.id} onSelect={() => run(action.perform)}>
                <action.icon aria-hidden />
                {action.label}
                {action.shortcut ? <CommandShortcut>{action.shortcut}</CommandShortcut> : null}
              </CommandItem>
            ))}
          </CommandGroup>
        ) : null}
      </CommandList>
    </CommandDialog>
  )
}
