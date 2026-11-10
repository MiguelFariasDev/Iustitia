'use client'

import { MessageSquareText } from 'lucide-react'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { useAuthStore } from '@/stores/authStore'
import { useAtendimentos } from '../hooks'
import { tabFilters } from '../tabs'
import type { AtendimentoTab } from '../types'
import { ChatPanel } from './ChatPanel'
import { ContextPanel } from './ContextPanel'
import { ConversaList } from './ConversaList'

/**
 * Inbox em três colunas — conversas, chat e contexto. A conversa aberta vive na URL
 * (`?c=<id>`): recarregar a página, compartilhar o link ou voltar no navegador mantém o lugar.
 *
 * Abaixo de `lg` não cabem três colunas: mostra a lista OU a conversa (com "Voltar"), e o contexto
 * só no layout largo.
 */
export function AtendimentosView() {
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const userId = useAuthStore((s) => s.user?.id)

  const selectedId = searchParams.get('c')
  const [tab, setTab] = useState<AtendimentoTab>('all')
  const [search, setSearch] = useState('')
  const [mine, setMine] = useState(false)

  const list = useAtendimentos({
    ...tabFilters(tab),
    search,
    assignedUserId: mine ? (userId ?? null) : null,
  })

  function select(id: string | null) {
    const params = new URLSearchParams(searchParams.toString())
    if (id) params.set('c', id)
    else params.delete('c')
    const query = params.toString()
    router.replace((query ? `${pathname}?${query}` : pathname) as never, { scroll: false })
  }

  return (
    <div className="grid h-[calc(100dvh-16.5rem)] min-h-[32rem] gap-4 md:h-[calc(100dvh-13rem)] lg:grid-cols-[20rem_minmax(0,1fr)] xl:grid-cols-[20rem_minmax(0,1fr)_18rem]">
      <div className={selectedId ? 'hidden min-h-0 lg:block' : 'min-h-0'}>
        <ConversaList
          tab={tab}
          onTabChange={setTab}
          search={search}
          onSearch={setSearch}
          mine={mine}
          onMineChange={setMine}
          items={list.data?.items}
          isLoading={list.isPending}
          error={list.error}
          onRetry={() => void list.refetch()}
          selectedId={selectedId}
          onSelect={select}
        />
      </div>

      <div className={selectedId ? 'min-h-0' : 'hidden min-h-0 lg:block'}>
        {selectedId ? (
          <ChatPanel key={selectedId} atendimentoId={selectedId} onBack={() => select(null)} />
        ) : (
          <EmptyState
            icon={MessageSquareText}
            title="Selecione uma conversa"
            description="Escolha um atendimento na lista para ver as mensagens e responder."
            className="h-full"
          />
        )}
      </div>

      {selectedId ? (
        <div className="hidden min-h-0 overflow-y-auto xl:block">
          <ContextPanel key={selectedId} atendimentoId={selectedId} />
        </div>
      ) : null}
    </div>
  )
}
