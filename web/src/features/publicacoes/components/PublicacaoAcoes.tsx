'use client'

import { Archive, CheckCheck, ExternalLink, Eye, Gavel } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { useArchivePublicacao, useMarkPublicacaoAsRead, useMarkPublicacaoAsTreated } from '../hooks'
import type { Publication } from '../types'

export function PublicacaoAcoes({ publication }: { publication: Publication }) {
  const markAsRead = useMarkPublicacaoAsRead()
  const markAsTreated = useMarkPublicacaoAsTreated()
  const archive = useArchivePublicacao()

  const busy = markAsRead.isPending || markAsTreated.isPending || archive.isPending
  // Espelha Publication.ChangeStatus: só a pendente vira tratada; só a não arquivada arquiva.
  const canTreat = publication.status === 'untreated'
  const canArchive = publication.status !== 'archived'

  return (
    <div
      className="flex flex-wrap items-center gap-2"
      role="group"
      aria-label="Ações da publicação"
    >
      <Button
        variant="outline"
        size="sm"
        disabled={busy || publication.isRead}
        onClick={() => markAsRead.mutate(publication.id)}
      >
        <Eye aria-hidden />
        Marcar como lida
      </Button>
      <Button
        size="sm"
        disabled={busy || !canTreat}
        onClick={() => markAsTreated.mutate(publication.id)}
      >
        <CheckCheck aria-hidden />
        Marcar como tratada
      </Button>
      <Button
        variant="outline"
        size="sm"
        disabled={busy || !canArchive}
        onClick={() => archive.mutate(publication.id)}
      >
        <Archive aria-hidden />
        Arquivar
      </Button>

      {publication.processId ? (
        <Button variant="ghost" size="sm" asChild>
          <Link href={`${routes.processes}/${publication.processId}` as never}>
            <Gavel aria-hidden />
            Ver processo
          </Link>
        </Button>
      ) : null}

      {publication.metadata?.link ? (
        <Button variant="ghost" size="sm" asChild>
          <a href={publication.metadata.link} target="_blank" rel="noopener noreferrer">
            <ExternalLink aria-hidden />
            Acessar no DJEN
            <span className="sr-only"> (abre em nova aba)</span>
          </a>
        </Button>
      ) : null}
    </div>
  )
}
