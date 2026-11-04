'use client'

import { useQuery } from '@tanstack/react-query'
import { Briefcase } from 'lucide-react'
import Link from 'next/link'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { routes } from '@/config/routes'
import { processosQuery } from '../../processos/queries'
import { areaDireitoLabels, statusProcessoMeta } from '../../processos/types'

export function ClienteProcessosList({ clienteId }: { clienteId: string }) {
  const { data, isPending, isError } = useQuery(processosQuery({ clienteId, pageSize: 50 }))

  if (isPending) return <LoadingSpinner label="Carregando processos…" />
  if (isError) {
    return (
      <p role="alert" className="text-sm text-destructive">
        Não foi possível carregar os processos.
      </p>
    )
  }
  if (data.items.length === 0) {
    return (
      <EmptyState
        icon={Briefcase}
        title="Nenhum processo para este cliente"
        description="Cadastre um processo e selecione este cliente."
      />
    )
  }

  return (
    <ul className="divide-y rounded-lg border">
      {data.items.map((p) => (
        <li key={p.id} className="flex flex-wrap items-center gap-3 p-3 text-sm">
          <Link
            href={`${routes.processes}/${p.id}` as never}
            className="font-mono text-secondary underline-offset-4 hover:underline"
          >
            {p.cnjNumber}
          </Link>
          <span className="text-muted-foreground">{areaDireitoLabels[p.area]}</span>
          {p.tribunal ? <span className="text-muted-foreground">{p.tribunal}</span> : null}
          <StatusBadge tone={statusProcessoMeta[p.status].tone} className="ml-auto">
            {statusProcessoMeta[p.status].label}
          </StatusBadge>
        </li>
      ))}
    </ul>
  )
}
