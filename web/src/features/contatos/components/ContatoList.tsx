'use client'

import type { ColumnDef } from '@tanstack/react-table'
import { Pencil, Plus, Trash2, UserX } from 'lucide-react'
import Link from 'next/link'
import { useMemo, useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { DemoNotice } from '@/components/shared/DemoNotice'
import { EmptyState } from '@/components/shared/EmptyState'
import { ErrorState } from '@/components/shared/ErrorState'
import { FilterChip } from '@/components/shared/FilterChip'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { useContatos, useDeleteContato } from '../hooks'
import { tiposContato, tipoContatoLabels, type Contato, type TipoContato } from '../types'

const tomPorTipo: Record<TipoContato, Tone> = {
  ParteContraria: 'danger',
  Testemunha: 'info',
  Fornecedor: 'warning',
  Outros: 'neutral',
}

export function ContatoList() {
  const [tipo, setTipo] = useState<TipoContato | null>(null)
  const [aExcluir, setAExcluir] = useState<Contato | null>(null)

  const { data, isPending, isError, error, refetch } = useContatos({ tipo })
  const { mutate: excluir, isPending: excluindo } = useDeleteContato()

  const colunas = useMemo<ColumnDef<Contato, unknown>[]>(
    () => [
      { accessorKey: 'nome', header: 'Nome' },
      {
        accessorKey: 'tipo',
        header: 'Tipo',
        cell: ({ row }) => (
          <StatusBadge tone={tomPorTipo[row.original.tipo]}>
            {tipoContatoLabels[row.original.tipo]}
          </StatusBadge>
        ),
      },
      {
        accessorKey: 'documento',
        header: 'Documento',
        cell: ({ row }) => (
          <span className="font-mono text-sm whitespace-nowrap">
            {row.original.documento ?? '—'}
          </span>
        ),
      },
      { accessorKey: 'email', header: 'E-mail', cell: ({ row }) => row.original.email ?? '—' },
      {
        accessorKey: 'telefone',
        header: 'Telefone',
        cell: ({ row }) => (
          <span className="whitespace-nowrap">{row.original.telefone ?? '—'}</span>
        ),
      },
      {
        id: 'acoes',
        header: () => <span className="sr-only">Ações</span>,
        enableSorting: false,
        cell: ({ row }) => (
          <div className="flex justify-end gap-1">
            <Button variant="ghost" size="icon" asChild>
              <Link
                href={`${routes.contacts}/${row.original.id}/editar` as never}
                aria-label={`Editar ${row.original.nome}`}
              >
                <Pencil aria-hidden />
              </Link>
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Excluir ${row.original.nome}`}
              onClick={() => setAExcluir(row.original)}
            >
              <Trash2 aria-hidden />
            </Button>
          </div>
        ),
      },
    ],
    []
  )

  if (isError) {
    return (
      <ErrorState
        error={error}
        title="Não foi possível carregar os contatos"
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <DemoNotice modulo="Contatos" />

      <DataTable
        caption="Contatos do escritório"
        columns={colunas}
        data={data ?? []}
        isLoading={isPending}
        getRowId={(c) => c.id}
        searchPlaceholder="Buscar por nome, documento ou e-mail"
        toolbar={
          <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrar por tipo">
            {tiposContato.map((valor) => (
              <FilterChip
                key={valor}
                active={tipo === valor}
                onClick={() => setTipo(tipo === valor ? null : valor)}
              >
                {tipoContatoLabels[valor]}
              </FilterChip>
            ))}
          </div>
        }
        emptyNode={
          <EmptyState
            icon={UserX}
            title={tipo ? 'Nenhum contato deste tipo' : 'Nenhum contato cadastrado'}
            description={
              tipo
                ? 'Remova o filtro ou cadastre um novo contato.'
                : 'Cadastre partes contrárias, testemunhas e fornecedores para tê-los à mão.'
            }
            action={
              <Button asChild>
                <Link href={`${routes.contacts}/novo` as never}>
                  <Plus aria-hidden />
                  Novo contato
                </Link>
              </Button>
            }
          />
        }
      />

      <ConfirmDialog
        open={aExcluir !== null}
        onOpenChange={(open) => {
          if (!open) setAExcluir(null)
        }}
        title="Excluir contato?"
        description={`${aExcluir?.nome ?? 'O contato'} será removido da lista. Esta ação não pode ser desfeita.`}
        confirmLabel="Excluir"
        destructive
        isPending={excluindo}
        onConfirm={() => {
          if (!aExcluir) return
          excluir(aExcluir.id, { onSettled: () => setAExcluir(null) })
        }}
      />
    </div>
  )
}
