'use client'

import { ChevronLeft, ChevronRight, Plus, Trash2 } from 'lucide-react'
import { useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
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
import { useTarefaActions, useTarefas } from '../hooks'
import { AnelPrazo } from './AnelPrazo'
import { NovaTarefaDialog } from './NovaTarefaDialog'

interface CartaoProps {
  tarefa: Tarefa
  agora: Date | null
  onMover: (destino: Coluna) => void
  onRemover: () => void
}

function Cartao({ tarefa, agora, onMover, onRemover }: CartaoProps) {
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
      {tarefa.processo ? (
        <p className="font-mono text-xs text-muted-foreground">{tarefa.processo}</p>
      ) : null}
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-xs text-muted-foreground">
          {tarefa.responsavel ? (
            <>
              <Avatar className="size-6">
                <AvatarFallback className="bg-muted text-[10px] font-semibold">
                  {getInitials(tarefa.responsavel)}
                </AvatarFallback>
              </Avatar>
              {tarefa.responsavel}
            </>
          ) : (
            'Sem responsável'
          )}
        </span>
        <span className="flex gap-1">
          <button
            type="button"
            onClick={onRemover}
            aria-label={`Excluir “${tarefa.titulo}”`}
            className="inline-flex size-7 items-center justify-center rounded-md border outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring"
          >
            <Trash2 className="size-4" aria-hidden />
          </button>
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
 * e soltar. Sem módulo de tarefas no servidor ainda: as tarefas ficam neste navegador.
 */
export function TarefasView() {
  const tarefas = useTarefas()
  const { mover, remover } = useTarefaActions()
  const searchParams = useSearchParams()
  const [novaAberta, setNovaAberta] = useState(searchParams.get('nova') === '1')
  const agora = useNow()
  const [sobre, setSobre] = useState<Coluna | null>(null)
  const [aviso, setAviso] = useState('')

  function moverPara(id: string, destino: Coluna) {
    const tarefa = tarefas.find((t) => t.id === id)
    if (!tarefa || tarefa.coluna === destino) return
    void mover(id, destino)
    // Leitor de tela: o cartão some de uma coluna e aparece em outra; sem isto ninguém é avisado.
    setAviso(`“${tarefa.titulo}” movida para ${rotuloColuna(destino)}.`)
  }

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground">Equipe</p>
          <h1 className="text-[33px] leading-tight font-semibold">Tarefas</h1>
        </div>
        <Button onClick={() => setNovaAberta(true)}>
          <Plus aria-hidden />
          Nova tarefa
        </Button>
      </header>

      <p role="note" className="rounded-lg border border-dashed px-3 py-2 text-sm text-muted-foreground">
        As tarefas são do escritório: toda a equipe vê o mesmo quadro.
      </p>

      <NovaTarefaDialog open={novaAberta} onClose={() => setNovaAberta(false)} />

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
                      onRemover={() => void remover(tarefa.id)}
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
