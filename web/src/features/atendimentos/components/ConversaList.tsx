'use client'

import { Inbox } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { SearchInput } from '@/components/shared/SearchInput'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { cn } from '@/lib/utils'
import { displayName, initials } from '../format'
import { tabLabels } from '../tabs'
import { atendimentoTabs, type Atendimento, type AtendimentoTab } from '../types'
import { AtendimentoStatusBadge } from './AtendimentoStatusBadge'

interface ConversaListProps {
  tab: AtendimentoTab
  onTabChange: (tab: AtendimentoTab) => void
  search: string
  onSearch: (value: string) => void
  mine: boolean
  onMineChange: (value: boolean) => void
  items: Atendimento[] | undefined
  isLoading: boolean
  error: unknown
  onRetry: () => void
  selectedId: string | null
  onSelect: (id: string) => void
}

function relativeTime(iso: string) {
  const diffMinutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000))
  if (diffMinutes < 1) return 'agora'
  if (diffMinutes < 60) return `${diffMinutes} min`
  const hours = Math.round(diffMinutes / 60)
  if (hours < 24) return `${hours} h`
  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })
}

/** Coluna da esquerda: a fila de conversas, com abas por status, busca e "meus atendimentos". */
export function ConversaList({
  tab,
  onTabChange,
  search,
  onSearch,
  mine,
  onMineChange,
  items,
  isLoading,
  error,
  onRetry,
  selectedId,
  onSelect,
}: ConversaListProps) {
  const list = error ? (
    <ErrorState error={error} onRetry={onRetry} title="Não foi possível carregar as conversas" />
  ) : isLoading ? (
    <div className="space-y-2" role="status" aria-label="Carregando conversas">
      {Array.from({ length: 5 }, (_, index) => (
        <Skeleton key={index} className="h-16 rounded-lg" />
      ))}
    </div>
  ) : items && items.length > 0 ? (
    <ul className="space-y-1">
      {items.map((item) => {
        const selected = item.id === selectedId
        const name = displayName(item)
        return (
          <li key={item.id}>
            <button
              type="button"
              onClick={() => onSelect(item.id)}
              aria-current={selected ? 'true' : undefined}
              className={cn(
                'flex w-full items-start gap-3 rounded-lg border border-transparent p-3 text-left transition-colors hover:bg-card/70 focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none',
                // Fundo CLARO (card) e não o bege de "accent": o badge "Aguardando" (âmbar sobre
                // translucidez) só atinge o contraste AA de 4,5:1 sobre fundo claro.
                selected && 'border-primary/40 bg-card shadow-xs'
              )}
            >
              <Avatar>
                <AvatarFallback>{initials(item)}</AvatarFallback>
              </Avatar>
              <span className="min-w-0 flex-1 space-y-1">
                <span className="flex items-center justify-between gap-2">
                  <span className="truncate text-sm font-medium">{name}</span>
                  <time
                    dateTime={item.lastMessageAt}
                    className="shrink-0 text-xs text-muted-foreground"
                  >
                    {relativeTime(item.lastMessageAt)}
                  </time>
                </span>
                <span className="block truncate text-xs text-muted-foreground">
                  {item.lastMessagePreview ?? 'Sem mensagens'}
                </span>
                <AtendimentoStatusBadge status={item.status} />
              </span>
            </button>
          </li>
        )
      })}
    </ul>
  ) : (
    <EmptyState
      icon={Inbox}
      title="Nenhuma conversa por aqui"
      description={
        search || tab !== 'all' || mine
          ? 'Nenhuma conversa corresponde aos filtros.'
          : 'Quando um cliente escrever no WhatsApp, a conversa aparece aqui.'
      }
    />
  )

  return (
    <section aria-label="Conversas" className="flex h-full min-h-0 flex-col gap-3">
      <SearchInput
        value={search}
        onSearch={onSearch}
        label="Buscar conversa"
        placeholder="Nome ou telefone"
        className="sm:max-w-none"
      />

      <Tabs
        value={tab}
        onValueChange={(next) => onTabChange(next as AtendimentoTab)}
        className="min-h-0 flex-1"
      >
        <TabsList aria-label="Filtrar por status" className="h-auto w-full flex-wrap justify-start">
          {atendimentoTabs.map((value) => (
            <TabsTrigger key={value} value={value} className="px-2 text-xs">
              {tabLabels[value]}
            </TabsTrigger>
          ))}
        </TabsList>

        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <input
            type="checkbox"
            checked={mine}
            onChange={(event) => onMineChange(event.target.checked)}
            className="size-4 accent-primary"
          />
          Somente os meus atendimentos
        </label>

        {/*
          Um TabsContent por aba, todos com a MESMA lista (já filtrada pela aba ativa): o Radix só
          monta o da aba ativa. É o que dá a cada aba o painel que o aria-controls dela referencia —
          sem os painéis, o atributo aponta para um id que não existe e o axe reprova (WCAG 4.1.2).
        */}
        {atendimentoTabs.map((value) => (
          <TabsContent key={value} value={value} className="min-h-0 flex-1 overflow-y-auto">
            {list}
          </TabsContent>
        ))}
      </Tabs>
    </section>
  )
}
