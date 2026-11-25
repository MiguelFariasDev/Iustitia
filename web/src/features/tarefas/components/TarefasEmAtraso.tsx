'use client'

import { AlertTriangle } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { diasDeAtraso, rotuloColuna, tarefasAtrasadas, type Tarefa } from '../kanban'

const MAXIMO = 5

/**
 * "Tarefas em atraso" do painel: as mais antigas primeiro, com o link para o quadro completo.
 * Recebe as tarefas já carregadas (o painel decide o que mostrar enquanto carrega).
 */
export function TarefasEmAtraso({
  tarefas,
  agora,
  isPending,
  isError,
  onRetry,
}: {
  tarefas: readonly Tarefa[]
  agora: Date | null
  isPending: boolean
  isError: boolean
  onRetry: () => void
}) {
  const atrasadas = agora ? tarefasAtrasadas(tarefas, agora) : []

  return (
    <section aria-labelledby="tarefas-atraso-titulo" className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 id="tarefas-atraso-titulo" className="flex items-center gap-2 text-lg font-semibold">
          <AlertTriangle className="size-4 text-destructive" aria-hidden />
          Tarefas em atraso
          {atrasadas.length > 0 ? (
            <span className="rounded-full bg-destructive/10 px-2 py-0.5 text-xs font-medium text-destructive">
              {atrasadas.length}
            </span>
          ) : null}
        </h2>
        <Button variant="link" size="sm" asChild>
          <Link href={routes.tasks as never}>Abrir o quadro</Link>
        </Button>
      </div>

      {isPending ? (
        <p className="text-sm text-muted-foreground">Carregando as tarefas…</p>
      ) : isError ? (
        <p role="alert" className="text-sm text-destructive">
          Não foi possível carregar as tarefas.{' '}
          <button type="button" onClick={onRetry} className="underline underline-offset-4">
            Tentar de novo
          </button>
        </p>
      ) : atrasadas.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nenhuma tarefa em atraso.</p>
      ) : (
        <ul className="divide-y rounded-lg border">
          {atrasadas.slice(0, MAXIMO).map((tarefa) => {
            const dias = agora ? diasDeAtraso(tarefa, agora) : 0

            return (
              <li key={tarefa.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2.5">
                <span className="min-w-0 flex-1 truncate text-sm font-medium">{tarefa.titulo}</span>
                {tarefa.processo ? (
                  <span className="font-mono text-xs text-muted-foreground">{tarefa.processo}</span>
                ) : null}
                <span className="text-xs text-muted-foreground">
                  {tarefa.responsavel || 'Sem responsável'} · {rotuloColuna(tarefa.coluna)}
                </span>
                <span className="rounded-sm bg-destructive/10 px-1.5 py-0.5 text-xs font-medium text-destructive">
                  {dias === 1 ? 'venceu ontem' : `há ${dias} dias`}
                </span>
              </li>
            )
          })}
          {atrasadas.length > MAXIMO ? (
            <li className="px-3 py-2 text-right text-xs text-muted-foreground">
              e mais {atrasadas.length - MAXIMO} no quadro de tarefas
            </li>
          ) : null}
        </ul>
      )}
    </section>
  )
}
