'use client'

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Archive, Pencil } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { AgendarNoProcessoButton } from '@/features/agenda/components/AgendarNoProcessoButton'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { clienteQuery } from '@/features/clientes/queries'
import { routes } from '@/config/routes'
import { formatCurrency, formatDate } from '@/lib/format'
import { useArchiveProcesso, useChangeProcessoStatus } from '../hooks'
import { processoQuery } from '../queries'
import { ProcessoAbas } from './ProcessoAbas'
import {
  areaDireitoLabels,
  statusProcesso,
  statusProcessoMeta,
  type StatusProcesso,
} from '../types'

export function ProcessoDetail({ processoId }: { processoId: string }) {
  const queryClient = useQueryClient()
  const { data: processo, isPending } = useQuery(processoQuery(processoId))
  const changeStatus = useChangeProcessoStatus(processoId)
  const archive = useArchiveProcesso(processoId)
  const [confirmOpen, setConfirmOpen] = useState(false)

  // Só busca o cliente depois de saber qual é: `enabled` evita uma requisição com id vazio
  // enquanto o processo carrega.
  const { data: cliente } = useQuery({
    ...clienteQuery(processo?.clienteId ?? ''),
    enabled: Boolean(processo?.clienteId),
  })

  if (isPending || !processo) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando processo…" />
      </div>
    )
  }

  function refetchDetail() {
    void queryClient.invalidateQueries({ queryKey: processoQuery(processoId).queryKey })
  }

  const meta = statusProcessoMeta[processo.status]
  const isArquivado = processo.status === 'Arquivado'

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge tone={meta.tone}>{meta.label}</StatusBadge>
        <span className="text-sm text-muted-foreground">{meta.descricao}</span>

        <div className="ml-auto flex flex-wrap items-center gap-2">
          {isArquivado ? null : (
            <Select
              value={processo.status}
              onValueChange={(value) =>
                changeStatus.mutate(value as StatusProcesso, { onSuccess: refetchDetail })
              }
              disabled={changeStatus.isPending}
            >
              <SelectTrigger className="w-40" aria-label="Alterar situação do processo">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {statusProcesso
                  .filter((value) => value !== 'Arquivado')
                  .map((value) => (
                    <SelectItem key={value} value={value}>
                      {statusProcessoMeta[value].label}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          )}

          <AgendarNoProcessoButton processoId={processo.id} />

          <Button variant="outline" size="sm" asChild>
            <Link href={`${routes.processes}/${processo.id}/editar` as never}>
              <Pencil aria-hidden />
              Editar
            </Link>
          </Button>

          {isArquivado ? null : (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmOpen(true)}
              disabled={archive.isPending}
            >
              <Archive aria-hidden />
              Arquivar
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Processo</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-2">
              <Field label="Número CNJ" mono>
                {processo.cnjNumber}
              </Field>
              <Field label="Área">{areaDireitoLabels[processo.area]}</Field>
              <Field label="Cliente">
                {cliente ? (
                  <Link
                    href={`${routes.clients}/${cliente.id}` as never}
                    className="text-secondary underline-offset-4 hover:underline"
                  >
                    {cliente.nome}
                  </Link>
                ) : null}
              </Field>
              <Field label="Distribuição">
                {processo.dataDistribuicao ? formatDate(processo.dataDistribuicao) : null}
              </Field>
              <Field label="Captura de publicações">
                {processo.geraCaptura ? 'Ativa' : 'Interrompida'}
              </Field>
              <Field label="Cadastrado em">{formatDate(processo.createdAt)}</Field>
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Foro</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-2">
              <Field label="Tribunal">{processo.tribunal}</Field>
              <Field label="Comarca">{processo.comarca}</Field>
              <Field label="Vara">{processo.vara}</Field>
            </dl>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Detalhes do caso</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-3">
              <Field label="Assunto">{processo.assunto}</Field>
              <Field label="Parte contrária">{processo.parteContraria}</Field>
              <Field label="Valor da causa">
                {processo.valorCausa === null ? null : formatCurrency(processo.valorCausa)}
              </Field>
            </dl>
            {processo.observacoes ? (
              <p className="mt-4 text-sm whitespace-pre-wrap">{processo.observacoes}</p>
            ) : null}
          </CardContent>
        </Card>
      </div>

      <ProcessoAbas processo={processo} />

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Arquivar este processo?"
        description="Arquivar é definitivo: o processo não volta a ficar ativo e as publicações dele deixam de ser capturadas. Se o caso apenas parou temporariamente, use a situação Suspenso — ela mantém a captura."
        confirmLabel="Arquivar"
        onConfirm={() => archive.mutate(undefined, { onSuccess: refetchDetail })}
      />
    </div>
  )
}

interface FieldProps {
  label: string
  mono?: boolean
  children: React.ReactNode
}

function Field({ label, mono, children }: FieldProps) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className={mono ? 'font-mono text-sm' : 'text-sm'}>{children || '—'}</dd>
    </div>
  )
}
