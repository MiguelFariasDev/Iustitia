'use client'

import { useQuery } from '@tanstack/react-query'
import { ListChecks, Newspaper } from 'lucide-react'
import Link from 'next/link'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { routes } from '@/config/routes'
import { useTarefaActions, useTarefas } from '@/features/tarefas/hooks'
import { rotuloColuna } from '@/features/tarefas/kanban'
import { ApiError } from '@/lib/api/errors'
import { formatDate } from '@/lib/format'
import { getPublicationsByProcess } from '../../publicacoes/api'
import { kindMeta, statusMeta } from '../../publicacoes/presentation'
import { useConsultarTribunal } from '../hooks'
import type { Processo } from '../types'

/**
 * Abas do detalhe. "Documentos" chega na Fase 4, e "Histórico" espera o filtro por entidade na
 * auditoria (hoje só existe por tipo) — abas vazias não entram.
 */
export function ProcessoAbas({
  processo,
  onNovaTarefa,
}: {
  processo: Processo
  onNovaTarefa: () => void
}) {
  return (
    <Tabs defaultValue="publicacoes">
      <TabsList>
        <TabsTrigger value="publicacoes">Publicações</TabsTrigger>
        <TabsTrigger value="movimentos">Movimentos</TabsTrigger>
        <TabsTrigger value="tarefas">Tarefas</TabsTrigger>
      </TabsList>
      <TabsContent value="publicacoes">
        <PublicacoesDoProcesso processoId={processo.id} />
      </TabsContent>
      <TabsContent value="tarefas">
        <TarefasDoProcesso processoId={processo.id} onNovaTarefa={onNovaTarefa} />
      </TabsContent>
      <TabsContent value="movimentos">
        <MovimentosDoProcesso cnjNumber={processo.cnjNumber} />
      </TabsContent>
    </Tabs>
  )
}

function PublicacoesDoProcesso({ processoId }: { processoId: string }) {
  const { data, isPending, isError } = useQuery({
    queryKey: ['publications', 'process', processoId],
    queryFn: () => getPublicationsByProcess(processoId),
  })

  if (isPending) return <LoadingSpinner label="Carregando publicações…" />
  if (isError) {
    return (
      <p role="alert" className="text-sm text-destructive">
        Não foi possível carregar as publicações.
      </p>
    )
  }
  if (data.length === 0) {
    return (
      <EmptyState
        icon={Newspaper}
        title="Nenhuma publicação capturada ainda"
        description="Quando o diário publicar algo sobre este processo, aparece aqui."
      />
    )
  }

  return (
    <ul className="divide-y rounded-lg border">
      {data.map((p) => (
        <li key={p.id} className="space-y-1.5 p-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge tone={statusMeta[p.status].tone}>{statusMeta[p.status].label}</StatusBadge>
            <span className="text-xs text-muted-foreground">{kindMeta[p.kind].label}</span>
            <span className="text-xs text-muted-foreground">{p.journal}</span>
            <time dateTime={p.publishedAt} className="ml-auto text-xs text-muted-foreground">
              {formatDate(p.publishedAt)}
            </time>
          </div>
          <p className="line-clamp-3 text-sm">{p.content}</p>
        </li>
      ))}
      <li className="p-3 text-right">
        <Button variant="link" size="sm" asChild>
          <Link href={routes.publications as never}>Abrir todas as publicações</Link>
        </Button>
      </li>
    </ul>
  )
}

/**
 * Movimentos vêm do DataJud e são buscados SOB DEMANDA: a consulta conta contra o limite de
 * 10/min e não deve disparar sozinha a cada abertura do processo.
 */
function MovimentosDoProcesso({ cnjNumber }: { cnjNumber: string }) {
  const consulta = useConsultarTribunal()
  const movimentos = consulta.data?.processo?.movimentos ?? []

  return (
    <Card>
      <CardContent className="space-y-4 pt-6">
        <div className="flex flex-wrap items-center gap-3">
          <Button
            size="sm"
            onClick={() => consulta.mutate(cnjNumber)}
            disabled={consulta.isPending}
          >
            {consulta.data ? 'Atualizar' : 'Buscar'} movimentos no tribunal
          </Button>
          <span className="text-xs text-muted-foreground">Fonte: DataJud (CNJ) — últimos 10.</span>
        </div>

        {consulta.isPending ? <LoadingSpinner label="Consultando o tribunal…" /> : null}
        {consulta.error ? (
          <p role="alert" className="text-sm text-destructive">
            {consulta.error instanceof ApiError
              ? consulta.error.message
              : 'Não foi possível consultar o tribunal agora.'}
          </p>
        ) : null}
        {consulta.data && !consulta.data.found ? (
          <p role="status" className="text-sm text-muted-foreground">
            Processo não encontrado na base pública do CNJ.
          </p>
        ) : null}

        {movimentos.length > 0 ? (
          <ol className="space-y-2 border-l pl-3">
            {movimentos.map((m, i) => (
              <li key={`${m.nome}-${i}`} className="text-sm">
                <span className="font-medium">{m.nome}</span>
                {m.dataHora ? (
                  <time dateTime={m.dataHora} className="ml-2 text-xs text-muted-foreground">
                    {formatDate(m.dataHora)}
                  </time>
                ) : null}
              </li>
            ))}
          </ol>
        ) : null}
      </CardContent>
    </Card>
  )
}

function TarefasDoProcesso({
  processoId,
  onNovaTarefa,
}: {
  processoId: string
  onNovaTarefa: () => void
}) {
  const tarefas = useTarefas().filter((t) => t.processoId === processoId)
  const { mover, remover } = useTarefaActions()

  if (tarefas.length === 0) {
    return (
      <EmptyState
        icon={ListChecks}
        title="Nenhuma tarefa neste processo"
        description="Crie tarefas para acompanhar o que falta fazer neste caso."
        action={
          <Button size="sm" onClick={onNovaTarefa}>
            Nova tarefa
          </Button>
        }
      />
    )
  }

  return (
    <Card>
      <CardContent className="divide-y p-0">
        {tarefas.map((tarefa) => {
          const concluida = tarefa.coluna === 'concluido'

          return (
            <div key={tarefa.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
              <input
                type="checkbox"
                checked={concluida}
                onChange={() => void mover(tarefa.id, concluida ? 'afazer' : 'concluido')}
                aria-label={`Concluir “${tarefa.titulo}”`}
                className="size-4"
              />
              <div className="min-w-0 flex-1">
                <p className={concluida ? 'text-sm line-through' : 'text-sm font-medium'}>
                  {tarefa.titulo}
                </p>
                <p className="text-xs text-muted-foreground">
                  {rotuloColuna(tarefa.coluna)}
                  {tarefa.prazo ? ` · prazo ${formatDate(`${tarefa.prazo}T12:00:00`)}` : ''}
                  {tarefa.responsavel ? ` · ${tarefa.responsavel}` : ''}
                </p>
              </div>
              <Button variant="ghost" size="sm" onClick={() => void remover(tarefa.id)}>
                Excluir
              </Button>
            </div>
          )
        })}
        <div className="px-4 py-3">
          <Button variant="outline" size="sm" onClick={onNovaTarefa}>
            Nova tarefa
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
