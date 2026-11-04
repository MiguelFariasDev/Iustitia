'use client'

import { useEffect, useRef } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { formatDateTime } from '@/lib/format'
import { useMarkPublicacaoAsRead, usePublicacao } from '../hooks'
import { statusMeta } from '../presentation'
import type { Publication } from '../types'
import { PublicacaoAcoes } from './PublicacaoAcoes'
import { PublicacaoInfoCard } from './PublicacaoInfoCard'
import { PublicacaoTextoCard } from './PublicacaoTextoCard'

export function PublicacaoDetail({ publicationId }: { publicationId: string }) {
  const { data, isPending, isError, error, refetch } = usePublicacao(publicationId)
  const markAsRead = useMarkPublicacaoAsRead()
  const autoMarked = useRef(false)

  // Abrir a publicação É lê-la. Marca uma única vez por visita (o ref evita repetir a cada
  // re-render e a cada refetch). Não é aprovação: o status de revisão não muda aqui.
  useEffect(() => {
    if (data && !data.isRead && !autoMarked.current) {
      autoMarked.current = true
      markAsRead.mutate(data.id)
    }
  }, [data, markAsRead])

  if (isPending) {
    return (
      <div className="space-y-4" role="status" aria-label="Carregando publicação">
        <Skeleton className="h-10 w-80 rounded-md" />
        <Skeleton className="h-48 rounded-xl" />
        <Skeleton className="h-72 rounded-xl" />
        <span className="sr-only">Carregando publicação…</span>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <ErrorState
        error={error}
        onRetry={() => void refetch()}
        title="Não foi possível carregar a publicação"
      />
    )
  }

  return (
    <div className="space-y-6">
      <PublicacaoAcoes publication={data} />

      <PublicacaoInfoCard publication={data} />

      <Tabs defaultValue="texto">
        <TabsList aria-label="Seções da publicação">
          <TabsTrigger value="texto">Texto bruto</TabsTrigger>
          <TabsTrigger value="metadados">Metadados</TabsTrigger>
          <TabsTrigger value="historico">Histórico</TabsTrigger>
        </TabsList>
        <TabsContent value="texto">
          <PublicacaoTextoCard content={data.content} />
        </TabsContent>
        <TabsContent value="metadados">
          <Metadados publication={data} />
        </TabsContent>
        <TabsContent value="historico">
          <Historico publication={data} />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function Metadados({ publication }: { publication: Publication }) {
  const entries = Object.entries(publication.metadata?.raw ?? {}).filter(
    ([, value]) => value !== null && value !== ''
  )

  if (entries.length === 0) {
    return (
      <p className="py-6 text-sm text-muted-foreground">Esta publicação não trouxe metadados.</p>
    )
  }

  return (
    <dl className="grid gap-3 rounded-xl border bg-card p-4 sm:grid-cols-2">
      {entries.map(([key, value]) => (
        <div key={key} className="min-w-0 space-y-0.5">
          <dt className="text-xs font-medium text-muted-foreground">{key}</dt>
          <dd className="text-sm break-words">
            {typeof value === 'string' ? value : JSON.stringify(value)}
          </dd>
        </div>
      ))}
    </dl>
  )
}

/**
 * Linha do tempo com o que o próprio registro sabe. O registro detalhado de auditoria (quem
 * fez cada alteração) existe no backend e é consultado por sócios em Configurações →
 * Auditoria — expô-lo aqui exigiria um filtro por entidade que a API ainda não tem.
 */
function Historico({ publication }: { publication: Publication }) {
  const events = [
    { label: 'Divulgada no diário', at: publication.publishedAt },
    { label: 'Capturada pelo sistema', at: publication.capturedAt },
  ]

  return (
    <div className="space-y-4 rounded-xl border bg-card p-4">
      <ol className="space-y-3">
        {events.map((event) => (
          <li key={event.label} className="flex items-baseline justify-between gap-4 text-sm">
            <span>{event.label}</span>
            <time dateTime={event.at} className="text-muted-foreground tabular">
              {formatDateTime(event.at)}
            </time>
          </li>
        ))}
        <li className="flex items-baseline justify-between gap-4 text-sm">
          <span>Situação atual</span>
          <span className="font-medium">{statusMeta[publication.status].label}</span>
        </li>
      </ol>
      {publication.reviewNotes ? (
        <p className="text-sm text-muted-foreground">
          Observação da revisão: {publication.reviewNotes}
        </p>
      ) : null}
      <p className="text-xs text-muted-foreground">
        O registro completo de quem alterou esta publicação fica em Configurações → Auditoria.
      </p>
    </div>
  )
}
