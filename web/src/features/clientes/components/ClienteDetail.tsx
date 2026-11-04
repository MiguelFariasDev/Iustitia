'use client'

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, RotateCcw, UserX } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { routes } from '@/config/routes'
import { formatDate } from '@/lib/format'
import { useDeactivateCliente, useReactivateCliente } from '../hooks'
import { clienteQuery } from '../queries'
import { ClienteProcessosList } from './ClienteProcessosList'
import { tipoClienteLabels, type Cliente } from '../types'

export function ClienteDetail({ clienteId }: { clienteId: string }) {
  const queryClient = useQueryClient()
  const { data: cliente, isPending } = useQuery(clienteQuery(clienteId))
  const deactivate = useDeactivateCliente()
  const reactivate = useReactivateCliente()
  const [confirmOpen, setConfirmOpen] = useState(false)

  if (isPending || !cliente) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando cliente…" />
      </div>
    )
  }

  function refetchDetail() {
    void queryClient.invalidateQueries({ queryKey: clienteQuery(clienteId).queryKey })
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge tone={cliente.isActive ? 'success' : 'neutral'}>
          {cliente.isActive ? 'Ativo' : 'Inativo'}
        </StatusBadge>
        <span className="text-sm text-muted-foreground">{tipoClienteLabels[cliente.tipo]}</span>

        <div className="ml-auto flex flex-wrap gap-2">
          <Button variant="outline" size="sm" asChild>
            <Link href={`${routes.clients}/${cliente.id}/editar` as never}>
              <Pencil aria-hidden />
              Editar
            </Link>
          </Button>

          {cliente.isActive ? (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmOpen(true)}
              disabled={deactivate.isPending}
            >
              <UserX aria-hidden />
              Desativar
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              disabled={reactivate.isPending}
              onClick={() => reactivate.mutate(cliente.id, { onSuccess: refetchDetail })}
            >
              <RotateCcw aria-hidden />
              Reativar
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Identificação</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-2">
              <Field label={cliente.tipo === 'PessoaFisica' ? 'Nome' : 'Razão social'}>
                {cliente.nome}
              </Field>
              <Field label={cliente.tipo === 'PessoaFisica' ? 'CPF' : 'CNPJ'} mono>
                {cliente.documento}
              </Field>
              {cliente.dataNascimento ? (
                <Field label="Data de nascimento">{formatDate(cliente.dataNascimento)}</Field>
              ) : null}
              <Field label="Cadastrado em">{formatDate(cliente.createdAt)}</Field>
            </dl>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Contato</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-2">
              <Field label="E-mail">{cliente.email}</Field>
              <Field label="Telefone">{cliente.telefone}</Field>
            </dl>
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Endereço</CardTitle>
          </CardHeader>
          <CardContent>
            <dl className="grid gap-3 sm:grid-cols-3">
              <Field label="Logradouro">{enderecoLinha(cliente)}</Field>
              <Field label="Bairro">{cliente.endereco.bairro}</Field>
              <Field label="Cidade/UF">
                {cliente.endereco.cidade
                  ? `${cliente.endereco.cidade}${cliente.endereco.uf ? `/${cliente.endereco.uf}` : ''}`
                  : null}
              </Field>
              <Field label="CEP">{cliente.endereco.cep}</Field>
            </dl>
          </CardContent>
        </Card>

        {cliente.observacoes ? (
          <Card className="lg:col-span-2">
            <CardHeader>
              <CardTitle className="text-base">Observações</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-sm whitespace-pre-wrap">{cliente.observacoes}</p>
            </CardContent>
          </Card>
        ) : null}
      </div>

      <section aria-labelledby="processos-heading" className="space-y-3">
        <h2 id="processos-heading" className="text-base font-semibold">
          Processos
        </h2>
        <ClienteProcessosList clienteId={cliente.id} />
      </section>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Desativar este cliente?"
        description="Ele deixa de aparecer nas buscas de novos processos. O histórico e os processos já cadastrados continuam disponíveis, e você pode reativá-lo depois."
        confirmLabel="Desativar"
        onConfirm={() => deactivate.mutate(cliente.id, { onSuccess: refetchDetail })}
      />
    </div>
  )
}

function enderecoLinha(cliente: Cliente) {
  const { logradouro, numero, complemento } = cliente.endereco
  if (!logradouro) return null

  return [logradouro, numero, complemento].filter(Boolean).join(', ')
}

interface FieldProps {
  label: string
  mono?: boolean
  children: React.ReactNode
}

/** Campo vazio mostra travessão em vez de sumir: a ausência do dado é informação. */
function Field({ label, mono, children }: FieldProps) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className={mono ? 'font-mono text-sm' : 'text-sm'}>{children || '—'}</dd>
    </div>
  )
}
