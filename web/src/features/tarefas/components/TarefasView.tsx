'use client'

import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { DemoNotice } from '@/components/shared/DemoNotice'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { useNow } from '@/hooks/useNow'
import { getInitials } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  colunas,
  colunaVizinha,
  progresso,
  rotuloColuna,
  type Coluna,
  type Tarefa,
} from '../kanban'
import { useTarefasStore } from '../store'
import { AnelPrazo } from './AnelPrazo'

interface CartaoProps {
  tarefa: Tarefa
  agora: Date | null
  onMover: (destino: Coluna) => void
}

function Cartao({ tarefa, agora, onMover }: CartaoProps) {
  const esquerda = colunaVizinha(tarefa.coluna, -1)
  const direita = colunaVizinha(tarefa.coluna, 1)
  const prog = agora ? progresso(tarefa, agora) : { pct: 0, tom: 'ok' as const, rotulo: '…' }

  return (
    <article
      draggable
      onDragStart={(event) => {
        event.dataTransfer.setData('text/plain', tarefa.id)
        event.dataTransfer.effectAllowed = 'move'
      }}
      className="cursor-grab space-y-3 rounded-[10px] border bg-card p-3.5 shadow-sm active:cursor-grabbing"
    >
      <div className="flex items-start justify-between gap-3">
        <h3 className="text-[17px] leading-snug font-medium">{tarefa.titulo}</h3>
        <AnelPrazo progresso={prog} />
      </div>
      <p className="font-mono text-xs text-muted-foreground">{tarefa.processo}</p>
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-xs text-muted-foreground">
          <Avatar className="size-6">
            <AvatarFallback className="bg-muted text-[10px] font-semibold">
              {getInitials(tarefa.responsavel)}
            </AvatarFallback>
          </Avatar>
          {tarefa.responsavel}
        </span>
        <span className="flex gap-1">
          {([-1, 1] as const).map((passo) => {
            const destino = passo === -1 ? esquerda : direita

            return (
              <button
                key={passo}
                type="button"
                disabled={destino === null}
                onClick={() => destino && onMover(destino)}
                aria-label={
                  destino
                    ? `Mover “${tarefa.titulo}” para ${rotuloColuna(destino)}`
                    : `${passo === -1 ? 'Já está na primeira' : 'Já está na última'} coluna`
                }
                className="inline-flex size-7 items-center justify-center rounded-md border outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-25"
              >
                {passo === -1 ? (
                  <ChevronLeft className="size-4" aria-hidden />
                ) : (
                  <ChevronRight className="size-4" aria-hidden />
                )}
              </button>
            )
          })}
        </span>
      </div>
    </article>
  )
}

/**
 * Kanban de equipe. Mover um cartão: botões ‹ › (caminho por teclado e leitor de tela) ou arrastar
 * e soltar. Não há módulo de tarefas no backend — tudo aqui é demonstração em memória.
 */
export function TarefasView() {
  const tarefas = useTarefasStore((s) => s.tarefas)
  const mover = useTarefasStore((s) => s.mover)
  const agora = useNow()
  const [sobre, setSobre] = useState<Coluna | null>(null)
  const [aviso, setAviso] = useState('')

  function moverPara(id: string, destino: Coluna) {
    const tarefa = tarefas.find((t) => t.id === id)
    if (!tarefa || tarefa.coluna === destino) return
    mover(id, destino)
    // Leitor de tela: o cartão some de uma coluna e aparece em outra; sem isto ninguém é avisado.
    setAviso(`“${tarefa.titulo}” movida para ${rotuloColuna(destino)}.`)
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <p className="text-xs font-medium text-muted-foreground">Equipe</p>
        <h1 className="text-[33px] leading-tight font-semibold">Kanban de equipe</h1>
      </header>

      <DemoNotice modulo="Tarefas" />

      <p className="sr-only" role="status" aria-live="polite">
        {aviso}
      </p>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {colunas.map((coluna) => {
          const cartoes = tarefas.filter((t) => t.coluna === coluna.id)

          return (
            <section
              key={coluna.id}
              aria-label={`${coluna.label}, ${cartoes.length} tarefas`}
              onDragOver={(event) => {
                event.preventDefault()
                setSobre(coluna.id)
              }}
              onDragLeave={() => setSobre((atual) => (atual === coluna.id ? null : atual))}
              onDrop={(event) => {
                event.preventDefault()
                setSobre(null)
                moverPara(event.dataTransfer.getData('text/plain'), coluna.id)
              }}
              className={cn(
                'min-h-40 space-y-3 rounded-2xl bg-muted p-3 transition-colors',
                sobre === coluna.id && 'bg-ia-soft'
              )}
            >
              <header className="flex items-center justify-between px-1">
                <h2 className="text-sm font-semibold">{coluna.label}</h2>
                <span className="font-mono text-xs text-muted-foreground" aria-hidden>
                  {cartoes.length}
                </span>
              </header>
              <ul className="space-y-3">
                {cartoes.map((tarefa) => (
                  <li key={tarefa.id}>
                    <Cartao
                      tarefa={tarefa}
                      agora={agora}
                      onMover={(destino) => moverPara(tarefa.id, destino)}
                    />
                  </li>
                ))}
              </ul>
              {cartoes.length === 0 ? (
                <p className="py-6 text-center text-sm text-muted-foreground">Nenhuma tarefa.</p>
              ) : null}
            </section>
          )
        })}
      </div>
    </div>
  )
}
