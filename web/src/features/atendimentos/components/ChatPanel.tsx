'use client'

import { ArrowLeft, Bot, CheckCheck, Hand, Lock, Send } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ErrorState } from '@/components/shared/ErrorState'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useAuthStore } from '@/stores/authStore'
import { displayName, formatPhone } from '../format'
import { useAtendimento, useChangeAtendimentoStatus, useMensagens, useSendMensagem } from '../hooks'
import { AtendimentoStatusBadge } from './AtendimentoStatusBadge'
import { MessageBubble } from './MessageBubble'

/** Limite do corpo de uma mensagem de texto na WhatsApp Cloud API (Atendimento.MaxBodyLength). */
export const MAX_MESSAGE_LENGTH = 4096

interface ChatPanelProps {
  /**
   * O chamador monta este componente com `key={atendimentoId}`: trocar de conversa remonta o painel
   * e zera o rascunho, para ele nunca ir parar na conversa de outro cliente.
   */
  atendimentoId: string
  /** Só no layout estreito: volta da conversa para a lista. */
  onBack?: () => void
}

/** Por que o campo de digitação está bloqueado — dito em voz alta, nunca só desabilitado. */
function composerHint(status: string, mine: boolean): string | null {
  if (status === 'Closed') return 'Atendimento encerrado.'
  if (status === 'PendingHuman') return 'Assuma o atendimento para responder ao cliente.'
  if (status === 'Bot') return 'O bot está conduzindo esta conversa.'
  if (status === 'Human' && !mine) return 'Esta conversa está com outro advogado.'
  return null
}

/**
 * Coluna central: cabeçalho com as ações do atendimento, a conversa e o campo de digitação.
 *
 * O campo SÓ é liberado quando o atendimento está em `Human` E é do advogado logado — é a mesma
 * regra do backend (falar assume a conversa e conflita com outro advogado), refletida na tela
 * para o usuário não digitar uma mensagem que seria recusada.
 */
export function ChatPanel({ atendimentoId, onBack }: ChatPanelProps) {
  const userId = useAuthStore((s) => s.user?.id)
  const atendimento = useAtendimento(atendimentoId)
  const mensagens = useMensagens(atendimentoId)
  const changeStatus = useChangeAtendimentoStatus(atendimentoId)
  const send = useSendMensagem(atendimentoId)

  const [draft, setDraft] = useState('')
  const [confirmClose, setConfirmClose] = useState(false)
  const logRef = useRef<HTMLDivElement>(null)

  const lastMessageId = mensagens.data?.items.at(-1)?.id

  // Rola para a mensagem mais nova quando chega uma (ou ao abrir a conversa).
  useEffect(() => {
    const log = logRef.current
    if (log) log.scrollTop = log.scrollHeight
  }, [lastMessageId, atendimentoId])

  if (atendimento.isError) {
    return (
      <ErrorState
        error={atendimento.error}
        onRetry={() => void atendimento.refetch()}
        title="Não foi possível carregar o atendimento"
      />
    )
  }

  if (!atendimento.data) {
    return (
      <Skeleton
        className="h-full min-h-96 rounded-xl"
        role="status"
        aria-label="Carregando conversa"
      />
    )
  }

  const current = atendimento.data
  const mine = current.status === 'Human' && current.assignedUserId === userId
  const canType = mine
  const hint = composerHint(current.status, mine)
  const trimmed = draft.trim()
  const busy = changeStatus.isPending || send.isPending

  function submit() {
    if (!canType || !trimmed || send.isPending) return
    send.mutate(trimmed, { onSuccess: () => setDraft('') })
  }

  return (
    <section
      aria-label="Conversa"
      className="flex h-full min-h-0 flex-col rounded-xl border bg-card"
    >
      <header className="flex flex-wrap items-center gap-3 border-b p-3">
        {onBack ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={onBack}
            aria-label="Voltar para a lista"
          >
            <ArrowLeft aria-hidden />
          </Button>
        ) : null}

        <div className="min-w-0 flex-1">
          <h2 className="truncate text-base font-semibold">{displayName(current)}</h2>
          <p className="truncate text-xs text-muted-foreground">
            {formatPhone(current.phoneNumber)}
          </p>
        </div>

        <AtendimentoStatusBadge status={current.status} />

        <div className="flex flex-wrap items-center gap-2">
          {current.status === 'PendingHuman' ? (
            <Button
              type="button"
              size="sm"
              disabled={busy}
              onClick={() => changeStatus.mutate('Human')}
            >
              <Hand aria-hidden /> Assumir atendimento
            </Button>
          ) : null}

          {mine ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={busy}
              onClick={() => changeStatus.mutate('Bot')}
            >
              <Bot aria-hidden /> Devolver ao bot
            </Button>
          ) : null}

          {current.status !== 'Closed' ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={busy}
              onClick={() => setConfirmClose(true)}
            >
              <CheckCheck aria-hidden /> Encerrar atendimento
            </Button>
          ) : null}
        </div>
      </header>

      {/*
        role="log": leitores de tela anunciam as mensagens novas sem roubar o foco. É um <div> (e não
        o <ul>) porque role="log" anula a semântica de lista do <ul> e deixaria os <li> órfãos. O
        tabIndex deixa a região rolável alcançável pelo teclado (WCAG 2.1.1) — ela não tem conteúdo
        focável por si.
      */}
      <div
        ref={logRef}
        role="log"
        aria-live="polite"
        aria-label="Mensagens da conversa"
        tabIndex={0}
        className="min-h-0 flex-1 overflow-y-auto p-4 focus-visible:ring-[3px] focus-visible:ring-ring/50 focus-visible:outline-none"
      >
        {mensagens.isError ? (
          <ErrorState
            error={mensagens.error}
            onRetry={() => void mensagens.refetch()}
            title="Não foi possível carregar as mensagens"
          />
        ) : mensagens.isPending ? (
          <div role="status" aria-label="Carregando mensagens">
            <Skeleton className="h-16 rounded-xl" />
          </div>
        ) : mensagens.data.items.length === 0 ? (
          <p className="py-8 text-center text-sm text-muted-foreground">Nenhuma mensagem ainda.</p>
        ) : (
          <>
            {mensagens.data.hasMore ? (
              <p className="pb-3 text-center text-xs text-muted-foreground">
                Exibindo as mensagens mais recentes.
              </p>
            ) : null}
            <ul className="space-y-3">
              {mensagens.data.items.map((mensagem) => (
                <MessageBubble key={mensagem.id} mensagem={mensagem} />
              ))}
            </ul>
          </>
        )}
      </div>

      <form
        className="space-y-2 border-t p-3"
        onSubmit={(event) => {
          event.preventDefault()
          submit()
        }}
      >
        {hint ? (
          <p className="flex items-center gap-2 text-xs text-muted-foreground" id="composer-hint">
            <Lock className="size-3" aria-hidden /> {hint}
          </p>
        ) : null}

        <div className="flex items-end gap-2">
          <Textarea
            aria-label="Mensagem para o cliente"
            aria-describedby={hint ? 'composer-hint' : undefined}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              // Enter envia; Shift+Enter quebra a linha. Ignora a composição de IME (acentos).
              if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
                event.preventDefault()
                submit()
              }
            }}
            disabled={!canType}
            maxLength={MAX_MESSAGE_LENGTH}
            rows={2}
            placeholder={canType ? 'Escreva uma mensagem…' : ''}
            className="max-h-40 min-h-0 resize-none"
          />
          <Button
            type="submit"
            disabled={!canType || !trimmed || send.isPending}
            aria-label="Enviar mensagem"
          >
            <Send aria-hidden /> Enviar
          </Button>
        </div>

        {canType && draft.length > MAX_MESSAGE_LENGTH - 200 ? (
          <p className="text-right text-xs text-muted-foreground" aria-live="polite">
            {draft.length}/{MAX_MESSAGE_LENGTH}
          </p>
        ) : null}
      </form>

      <ConfirmDialog
        open={confirmClose}
        onOpenChange={setConfirmClose}
        title="Encerrar este atendimento?"
        description="O bot não responde mais nesta conversa. Se o cliente escrever de novo, um novo atendimento será aberto."
        confirmLabel="Encerrar atendimento"
        destructive
        isPending={changeStatus.isPending}
        onConfirm={() =>
          changeStatus.mutate('Closed', {
            onSuccess: () => setConfirmClose(false),
            onError: () => setConfirmClose(false),
          })
        }
      />
    </section>
  )
}
