import Link from 'next/link'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { routes } from '@/config/routes'
import { formatDate, formatDateTime } from '@/lib/format'
import { kindMeta } from '../presentation'
import type { Publication } from '../types'
import { PublicacaoStatusBadge } from './PublicacaoStatusBadge'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-0.5">
      <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  )
}

export function PublicacaoInfoCard({ publication }: { publication: Publication }) {
  const kind = kindMeta[publication.kind]

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">
          <h2>Dados da publicação</h2>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2">
          <Field label="Divulgada em">
            <time dateTime={publication.publishedAt}>{formatDate(publication.publishedAt)}</time>
          </Field>
          <Field label="Capturada em">
            <time dateTime={publication.capturedAt}>{formatDateTime(publication.capturedAt)}</time>
          </Field>
          <Field label="Tipo">{kind.label}</Field>
          <Field label="Tribunal">
            {publication.tribunal ?? '—'} · {publication.journal}
          </Field>
          <Field label="Órgão julgador">{publication.metadata?.orgao ?? '—'}</Field>
          <Field label="Classe">{publication.metadata?.classe ?? '—'}</Field>
          <Field label="Processo">
            {publication.processId && publication.processNumber ? (
              <Link
                href={`${routes.processes}/${publication.processId}` as never}
                className="font-mono text-xs underline-offset-4 hover:underline"
              >
                {publication.processNumber}
              </Link>
            ) : (
              <span className="font-mono text-xs">{publication.processNumber ?? '—'}</span>
            )}
          </Field>
          <Field label="Cliente">
            {publication.clienteId && publication.clienteNome ? (
              <Link
                href={`${routes.clients}/${publication.clienteId}` as never}
                className="underline-offset-4 hover:underline"
              >
                {publication.clienteNome}
              </Link>
            ) : (
              '—'
            )}
          </Field>
          <Field label="Situação">
            <span className="flex flex-wrap items-center gap-1.5">
              <PublicacaoStatusBadge publication={publication} />
              <StatusBadge tone={publication.isRead ? 'neutral' : 'brand'} hideDot>
                {publication.isRead ? 'Lida' : 'Não lida'}
              </StatusBadge>
            </span>
          </Field>
          {publication.reviewNotes ? (
            <div className="sm:col-span-2">
              <Field label="Observação da revisão">{publication.reviewNotes}</Field>
            </div>
          ) : null}
        </dl>
      </CardContent>
    </Card>
  )
}
