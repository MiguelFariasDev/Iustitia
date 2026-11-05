import { AlertCircle, Bot, Clock, Scale, User } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Mensagem, MensagemSender } from '../types'

const senderLabel: Record<MensagemSender, string> = {
  Client: 'Cliente',
  Bot: 'Bot',
  Human: 'Escritório',
}

const senderIcon = { Client: User, Bot, Human: Scale } as const

function time(iso: string) {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

/**
 * Cliente à esquerda, escritório (bot e advogado) à direita. Quem escreveu é dito em TEXTO, não só
 * por cor/posição. Mensagem que o Bot não conseguiu entregar ao WhatsApp é sinalizada — o
 * advogado precisa saber que o cliente NÃO a recebeu.
 */
export function MessageBubble({ mensagem }: { mensagem: Mensagem }) {
  const outgoing = mensagem.direction === 'Outbound'
  const Icon = senderIcon[mensagem.sender]

  return (
    <li className={cn('flex', outgoing ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'max-w-[80%] space-y-1 rounded-2xl px-3 py-2 text-sm shadow-xs',
          outgoing
            ? mensagem.sender === 'Bot'
              ? 'rounded-br-sm bg-muted text-foreground'
              : 'rounded-br-sm bg-primary text-primary-foreground'
            : 'rounded-bl-sm border bg-card text-card-foreground'
        )}
      >
        <p className="flex items-center gap-1 text-xs opacity-80">
          <Icon className="size-3" aria-hidden />
          {senderLabel[mensagem.sender]}
        </p>
        {/* whitespace-pre-wrap: o menu do bot usa quebras de linha. Texto puro — nunca HTML. */}
        <p className="break-words whitespace-pre-wrap">{mensagem.body}</p>
        <p className="flex items-center justify-end gap-1 text-xs opacity-80">
          {mensagem.delivery === 'Pending' ? (
            <>
              <Clock className="size-3" aria-hidden /> Enviando…
            </>
          ) : null}
          {mensagem.delivery === 'Failed' ? (
            <span
              role="alert"
              className="flex items-center gap-1 font-medium text-destructive-foreground"
            >
              <AlertCircle className="size-3" aria-hidden /> Falhou ao enviar — o cliente não
              recebeu
            </span>
          ) : null}
          <time dateTime={mensagem.createdAt}>{time(mensagem.createdAt)}</time>
        </p>
      </div>
    </li>
  )
}
