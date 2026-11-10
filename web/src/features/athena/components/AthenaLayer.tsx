'use client'

import { Send, Sparkles } from 'lucide-react'
import { useId, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import { env } from '@/config/env'
import { useAgenda } from '@/features/agenda/hooks'
import { eventosDoDia, periodFor, startOfDay } from '@/features/agenda/dates'
import { useAccess } from '@/hooks/useAccess'
import { useNow } from '@/hooks/useNow'
import { primeiroNome } from '@/features/dashboard/time'
import { useAuthStore } from '@/stores/authStore'
import { CHIPS, RESPOSTA_SEM_IA, respostaPrazosDeHoje, saudacaoInicial } from '../respostas'

interface Mensagem {
  autor: 'athena' | 'usuario'
  texto: string
}

/**
 * Botão flutuante + painel da Athena. DEMONSTRAÇÃO: não há serviço de IA. Só aparece com mocks
 * ligados e para quem acessa processos (a API de IA exige LawyerOrAbove). As respostas vêm de
 * `respostas.ts`; "Prazos de hoje" usa a agenda real.
 */
export function AthenaLayer() {
  const access = useAccess()
  const [aberto, setAberto] = useState(false)

  if (!env.useMocks || !access.isReady || !access.can('processes')) return null

  return (
    <Sheet open={aberto} onOpenChange={setAberto}>
      <SheetTrigger asChild>
        <button
          type="button"
          aria-label="Abrir a Athena, assistente de IA"
          className="fixed right-4 bottom-20 z-40 flex size-12 iu-lift items-center justify-center rounded-full bg-ia text-white shadow-lg focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none md:right-6 md:bottom-6"
        >
          <Sparkles className="size-5" aria-hidden />
        </button>
      </SheetTrigger>
      <SheetContent
        side="right"
        className="w-full gap-0 border-l-0 bg-athena p-0 text-athena-foreground data-[state=closed]:duration-150 data-[state=open]:duration-[250ms] sm:max-w-[410px]"
      >
        <AthenaPanel aberto={aberto} />
      </SheetContent>
    </Sheet>
  )
}

function AthenaPanel({ aberto }: { aberto: boolean }) {
  const nome = primeiroNome(useAuthStore((s) => s.user?.name))
  const now = useNow()
  const campoId = useId()
  const fim = useRef<HTMLDivElement>(null)
  const [mensagens, setMensagens] = useState<Mensagem[]>(() => [
    { autor: 'athena', texto: saudacaoInicial(nome) },
  ])
  const [texto, setTexto] = useState('')

  const hoje = now ? startOfDay(now) : null
  const agenda = useAgenda(periodFor('dia', hoje ?? new Date(0)), null, {
    enabled: aberto && hoje !== null,
  })

  const responder = (pergunta: string, resposta: string) => {
    setMensagens((atual) => [
      ...atual,
      { autor: 'usuario', texto: pergunta },
      { autor: 'athena', texto: resposta },
    ])
    queueMicrotask(() => fim.current?.scrollIntoView?.({ block: 'end' }))
  }

  const perguntar = (pergunta: string) => {
    if (pergunta === 'Prazos de hoje' && hoje && agenda.data) {
      const prazos = eventosDoDia(agenda.data, hoje).filter((e) => e.tipo === 'Prazo')
      responder(pergunta, respostaPrazosDeHoje(prazos))
      return
    }
    responder(pergunta, RESPOSTA_SEM_IA)
  }

  return (
    <div className="flex h-full flex-col">
      <header className="border-b border-white/10 py-3 pr-12 pl-4">
        <SheetTitle className="flex items-center gap-2 text-athena-foreground">
          <Sparkles className="size-4 text-ia-soft" aria-hidden /> Athena
        </SheetTitle>
        <SheetDescription className="text-xs text-athena-muted">
          Demonstração · sem conexão com o serviço de IA
        </SheetDescription>
      </header>

      <div
        className="flex-1 space-y-3 overflow-y-auto p-4"
        role="log"
        aria-label="Conversa com a Athena"
      >
        {mensagens.map((m, i) => (
          <p
            key={i}
            className={
              m.autor === 'athena'
                ? 'max-w-[90%] rounded-lg bg-athena-field px-3 py-2 text-sm whitespace-pre-line'
                : 'ml-auto max-w-[90%] rounded-lg bg-ia px-3 py-2 text-sm text-white'
            }
          >
            {m.texto}
          </p>
        ))}
        <div ref={fim} />
      </div>

      <div className="space-y-2 border-t border-white/10 p-3">
        <div className="flex flex-wrap gap-2">
          {CHIPS.map((chip) => (
            <button
              key={chip}
              type="button"
              disabled={chip === 'Prazos de hoje' && !agenda.data}
              onClick={() => perguntar(chip)}
              className="rounded-full border border-white/15 px-3 py-1 text-xs text-athena-foreground hover:bg-athena-field focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none disabled:opacity-50"
            >
              {chip}
            </button>
          ))}
        </div>
        <form
          className="flex gap-2"
          onSubmit={(event) => {
            event.preventDefault()
            const pergunta = texto.trim()
            if (!pergunta) return
            setTexto('')
            perguntar(pergunta)
          }}
        >
          <label htmlFor={campoId} className="sr-only">
            Pergunte à Athena
          </label>
          <input
            id={campoId}
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            placeholder="Pergunte à Athena…"
            className="min-w-0 flex-1 rounded-md bg-athena-field px-3 py-2 text-sm text-athena-foreground placeholder:text-athena-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
          />
          <Button type="submit" size="icon" aria-label="Enviar" disabled={!texto.trim()}>
            <Send className="size-4" aria-hidden />
          </Button>
        </form>
      </div>
    </div>
  )
}
