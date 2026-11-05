'use client'

import { useQueries } from '@tanstack/react-query'
import Link from 'next/link'
import { useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import { useMoveProcessoStatus } from '../hooks'
import { colunasKanban, isColunaKanban, KANBAN_PAGE_SIZE } from '../kanban'
import { processosQuery } from '../queries'
import {
  areaDireitoLabels,
  statusProcessoMeta,
  type ProcessoListItem,
  type ProcessosFilters,
  type StatusProcesso,
} from '../types'

interface ProcessosKanbanProps {
  /** Os mesmos filtros da lista, menos paginação e situação (a situação é a própria coluna). */
  filters: Omit<ProcessosFilters, 'page' | 'pageSize' | 'status'>
}

/**
 * Quadro de processos por situação. Arrastar um cartão para outra coluna chama o mesmo
 * endpoint de mudança de situação da tela de detalhe (PATCH /processos/{id}/status), então as
 * regras de transição continuam sendo as do servidor — se ele recusar, o cartão volta.
 *
 * Arrastar exige ponteiro, então cada cartão traz também um seletor "Mover para", que é o
 * caminho por teclado e leitor de tela.
 */
export function ProcessosKanban({ filters }: ProcessosKanbanProps) {
  const move = useMoveProcessoStatus()
  const [sobre, setSobre] = useState<StatusProcesso | null>(null)

  const colunas = useQueries({
    queries: colunasKanban.map((status) => ({
      ...processosQuery({ ...filters, status, page: 1, pageSize: KANBAN_PAGE_SIZE }),
      placeholderData: <T,>(previous: T | undefined) => previous,
    })),
  })

  const todos = colunas.flatMap((coluna) => coluna.data?.items ?? [])

  function mover(id: string, destino: StatusProcesso) {
    const card = todos.find((item) => item.id === id)
    if (!card || card.status === destino) return
    move.mutate({ id, status: destino })
  }

  if (colunas.every((coluna) => coluna.isPending)) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando quadro…" />
      </div>
    )
  }

  return (
    <div className="grid gap-4 lg:grid-cols-3">
      {colunasKanban.map((status, index) => {
        const coluna = colunas[index]!
        const items = coluna.data?.items ?? []
        const total = coluna.data?.totalCount ?? 0
        const meta = statusProcessoMeta[status]

        return (
          <section
            key={status}
            aria-label={`Processos ${meta.label.toLowerCase()}s`}
            onDragOver={(event) => {
              event.preventDefault()
              setSobre(status)
            }}
            onDragLeave={() => setSobre((atual) => (atual === status ? null : atual))}
            onDrop={(event) => {
              event.preventDefault()
              setSobre(null)
              mover(event.dataTransfer.getData('text/plain'), status)
            }}
            className={cn(
              'min-h-48 space-y-3 rounded-xl border bg-muted/30 p-3 transition-colors',
              sobre === status && 'border-primary bg-primary/5'
            )}
          >
            <header className="flex items-center justify-between gap-2">
              <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
              <span className="text-xs text-muted-foreground" aria-label={`${total} processos`}>
                {total}
              </span>
            </header>
            <p className="text-xs text-muted-foreground">{meta.descricao}</p>

            <ul className="space-y-2">
              {items.map((processo) => (
                <li key={processo.id}>
                  <CartaoProcesso
                    processo={processo}
                    onMove={(destino) => mover(processo.id, destino)}
                  />
                </li>
              ))}
            </ul>

            {items.length === 0 ? (
              <p className="py-6 text-center text-sm text-muted-foreground">Nenhum processo.</p>
            ) : null}
            {total > items.length ? (
              <p role="status" className="text-xs text-muted-foreground">
                Mostrando {items.length} de {total}. Use os filtros para refinar.
              </p>
            ) : null}
          </section>
        )
      })}
    </div>
  )
}

function CartaoProcesso({
  processo,
  onMove,
}: {
  processo: ProcessoListItem
  onMove: (destino: StatusProcesso) => void
}) {
  return (
    <article
      draggable
      onDragStart={(event) => {
        event.dataTransfer.setData('text/plain', processo.id)
        event.dataTransfer.effectAllowed = 'move'
      }}
      className="cursor-grab space-y-2 rounded-lg border bg-card p-3 shadow-xs active:cursor-grabbing"
    >
      <Link
        href={`${routes.processes}/${processo.id}` as never}
        className="font-mono text-sm font-medium text-secondary underline-offset-4 hover:underline"
      >
        {processo.cnjNumber}
      </Link>
      <p className="text-xs">
        {areaDireitoLabels[processo.area]}
        {processo.tribunal ? ` · ${processo.tribunal}` : ''}
      </p>
      {processo.parteContraria ? (
        <p className="truncate text-xs text-muted-foreground">{processo.parteContraria}</p>
      ) : null}

      <Select
        value={processo.status}
        onValueChange={(valor) => {
          if (isColunaKanban(valor)) onMove(valor)
        }}
      >
        <SelectTrigger
          size="sm"
          className="h-7 w-full text-xs"
          aria-label={`Mover ${processo.cnjNumber} para`}
        >
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {colunasKanban.map((status) => (
            <SelectItem key={status} value={status}>
              {statusProcessoMeta[status].label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </article>
  )
}
