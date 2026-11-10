'use client'

import { useQuery } from '@tanstack/react-query'
import Link from 'next/link'
import { Briefcase, UserRound } from 'lucide-react'
import { ErrorState } from '@/components/shared/ErrorState'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { clienteQuery } from '@/features/clientes/queries'
import { processosQuery } from '@/features/processos/queries'
import { statusProcessoMeta } from '@/features/processos/types'
import { useAuthStore } from '@/stores/authStore'
import { displayName, formatPhone } from '../format'
import { useAtendimento } from '../hooks'
import { AtendimentoStatusBadge } from './AtendimentoStatusBadge'
import { ClienteLinker } from './ClienteLinker'

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-sm break-words">{children}</dd>
    </div>
  )
}

function ClienteSection({ clienteId }: { clienteId: string }) {
  const cliente = useQuery(clienteQuery(clienteId))
  const processos = useQuery(processosQuery({ clienteId, pageSize: 5 }))

  return (
    <div className="space-y-4">
      {cliente.isError ? (
        <ErrorState
          error={cliente.error}
          onRetry={() => void cliente.refetch()}
          title="Não foi possível carregar o cliente"
        />
      ) : cliente.isPending ? (
        <Skeleton className="h-24 rounded-lg" role="status" aria-label="Carregando cliente" />
      ) : (
        <dl className="space-y-2">
          <Field label="Cliente">
            <Link
              href={`${routes.clients}/${cliente.data.id}` as never}
              className="font-medium underline-offset-4 hover:underline"
            >
              {cliente.data.nome}
            </Link>
          </Field>
          <Field label="Documento">{cliente.data.documento}</Field>
          {cliente.data.email ? <Field label="E-mail">{cliente.data.email}</Field> : null}
          {cliente.data.telefone ? (
            <Field label="Telefone cadastrado">{cliente.data.telefone}</Field>
          ) : null}
        </dl>
      )}

      <div className="space-y-2">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <Briefcase className="size-4" aria-hidden /> Processos
        </h3>
        {processos.isError ? (
          <ErrorState
            error={processos.error}
            onRetry={() => void processos.refetch()}
            title="Não foi possível carregar os processos"
          />
        ) : processos.isPending ? (
          <Skeleton className="h-16 rounded-lg" role="status" aria-label="Carregando processos" />
        ) : processos.data.items.length === 0 ? (
          <p className="text-xs text-muted-foreground">Este cliente ainda não tem processos.</p>
        ) : (
          <ul className="space-y-1">
            {processos.data.items.map((processo) => (
              <li key={processo.id} className="text-sm">
                <Link
                  href={`${routes.processes}/${processo.id}` as never}
                  className="block font-mono text-xs underline-offset-4 hover:underline"
                >
                  {processo.cnjNumber}
                </Link>
                <span className="text-xs text-muted-foreground">
                  {statusProcessoMeta[processo.status].label}
                  {processo.tribunal ? ` · ${processo.tribunal}` : ''}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

/** Coluna da direita: quem é o contato, a situação do atendimento e o que o escritório sabe dele. */
export function ContextPanel({ atendimentoId }: { atendimentoId: string }) {
  const userId = useAuthStore((s) => s.user?.id)
  const atendimento = useAtendimento(atendimentoId)

  if (atendimento.isError) {
    return (
      <ErrorState
        error={atendimento.error}
        onRetry={() => void atendimento.refetch()}
        title="Não foi possível carregar o contexto"
      />
    )
  }

  if (!atendimento.data) {
    return <Skeleton className="h-64 rounded-xl" role="status" aria-label="Carregando contexto" />
  }

  const current = atendimento.data

  return (
    <aside
      aria-label="Contexto do atendimento"
      className="animate-iu-in space-y-5 rounded-xl border bg-card p-4"
    >
      <section className="space-y-3">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <UserRound className="size-4" aria-hidden /> Contato
        </h3>
        <dl className="space-y-2">
          <Field label="Nome no WhatsApp">{displayName(current)}</Field>
          <Field label="Telefone">{formatPhone(current.phoneNumber)}</Field>
          <Field label="Situação">
            <AtendimentoStatusBadge status={current.status} />
          </Field>
          {current.status === 'Human' ? (
            <Field label="Responsável">
              {current.assignedUserId === userId ? 'Você' : 'Outro advogado'}
            </Field>
          ) : null}
        </dl>
      </section>

      <section aria-label="Cliente vinculado" className="space-y-3 border-t pt-4">
        {current.clientId ? (
          <ClienteSection clienteId={current.clientId} />
        ) : (
          <div className="space-y-3">
            <div>
              <h3 className="text-sm font-semibold">Contato sem cadastro</h3>
              <p className="text-xs text-muted-foreground">
                Vincule esta conversa a um cliente para ver os dados e os processos dele aqui.
              </p>
            </div>
            <ClienteLinker atendimentoId={current.id} />
          </div>
        )}
      </section>
    </aside>
  )
}
