'use client'

import { useQuery } from '@tanstack/react-query'
import { Newspaper } from 'lucide-react'
import Link from 'next/link'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { routes } from '@/config/routes'
import { ApiError } from '@/lib/api/errors'
import { formatDate } from '@/lib/format'
import { getPublicationsByProcess } from '../../publicacoes/api'
import { kindMeta, statusMeta } from '../../publicacoes/presentation'
import { useConsultarTribunal } from '../hooks'
import type { Processo } from '../types'

/**
 * Abas do detalhe. "Tarefas" e "Documentos" chegam nas Fases 3 e 4, e "Histórico" espera o
 * filtro por entidade na auditoria (hoje só existe por tipo) — abas vazias não entram.
 */
export function ProcessoAbas({ processo }: { processo: Processo }) {
  return (
    <Tabs defaultValue="publicacoes">
      <TabsList>
        <TabsTrigger value="publicacoes">Publicações</TabsTrigger>
        <TabsTrigger value="movimentos">Movimentos</TabsTrigger>
      </TabsList>
      <TabsContent value="publicacoes">
        <PublicacoesDoProcesso processoId={processo.id} />
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
