'use client'

import { useQuery } from '@tanstack/react-query'
import { Building2, Plus, User, UserX } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { FilterChip } from '@/components/shared/FilterChip'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Pagination } from '@/components/shared/Pagination'
import { SearchInput } from '@/components/shared/SearchInput'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import { clientesQuery } from '../queries'
import { tipoClienteLabels, type ClientesFilters, type TipoCliente } from '../types'

const tipoFilters: TipoCliente[] = ['PessoaFisica', 'PessoaJuridica']

export function ClientesView() {
  const [filters, setFilters] = useState<ClientesFilters>({ page: 1, apenasAtivos: true })

  // useQuery (não useSuspenseQuery): trocar de filtro ou de página mantém a tabela anterior
  // na tela enquanto a nova chega, em vez de piscar o skeleton a cada clique.
  const { data, isPending, isFetching } = useQuery({
    ...clientesQuery(filters),
    placeholderData: (previous) => previous,
  })

  // Qualquer mudança de filtro volta para a página 1: continuar na página 5 de um resultado
  // que agora tem 2 páginas mostraria uma lista vazia sem explicação.
  function updateFilters(patch: Partial<ClientesFilters>) {
    setFilters((current) => ({ ...current, ...patch, page: 1 }))
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <SearchInput
          onSearch={(busca) => updateFilters({ busca })}
          label="Pesquisar clientes"
          placeholder="Nome ou CPF/CNPJ…"
          className="sm:max-w-sm"
        />

        <div className="flex flex-wrap items-center gap-2">
          <div className="flex flex-wrap gap-2" role="group" aria-label="Filtrar por tipo">
            {tipoFilters.map((value) => {
              const active = filters.tipo === value
              return (
                <FilterChip
                  key={value}
                  active={active}
                  onClick={() => updateFilters({ tipo: active ? null : value })}
                >
                  {tipoClienteLabels[value]}
                </FilterChip>
              )
            })}
            <FilterChip
              active={filters.apenasAtivos === false}
              onClick={() =>
                updateFilters({ apenasAtivos: filters.apenasAtivos === false ? true : false })
              }
            >
              Inativos
            </FilterChip>
          </div>

          <Button asChild size="sm">
            <Link href={`${routes.clients}/novo`}>
              <Plus aria-hidden />
              Novo cliente
            </Link>
          </Button>
        </div>
      </div>

      {isPending ? (
        <div className="flex justify-center py-12">
          <LoadingSpinner label="Carregando clientes…" />
        </div>
      ) : data && data.items.length > 0 ? (
        <>
          <div
            className={cn(
              'rounded-xl border transition-opacity',
              isFetching ? 'opacity-60' : 'opacity-100'
            )}
            aria-busy={isFetching}
          >
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nome</TableHead>
                  <TableHead>Documento</TableHead>
                  <TableHead className="hidden md:table-cell">Contato</TableHead>
                  <TableHead>Situação</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((cliente) => (
                  <TableRow key={cliente.id}>
                    <TableCell>
                      <Link
                        href={`${routes.clients}/${cliente.id}` as never}
                        className="font-medium text-secondary underline-offset-4 hover:underline"
                      >
                        {cliente.nome}
                      </Link>
                      <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
                        {cliente.tipo === 'PessoaFisica' ? (
                          <User className="size-3" aria-hidden />
                        ) : (
                          <Building2 className="size-3" aria-hidden />
                        )}
                        {tipoClienteLabels[cliente.tipo]}
                      </p>
                    </TableCell>
                    <TableCell className="font-mono text-sm whitespace-nowrap">
                      {cliente.documento}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground md:table-cell">
                      {cliente.email ?? cliente.telefone ?? '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge tone={cliente.isActive ? 'success' : 'neutral'}>
                        {cliente.isActive ? 'Ativo' : 'Inativo'}
                      </StatusBadge>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          <Pagination
            page={data.page}
            pageCount={data.totalPages}
            totalCount={data.totalCount}
            onPageChange={(page) => setFilters((current) => ({ ...current, page }))}
          />
        </>
      ) : (
        <EmptyState
          icon={UserX}
          title={filters.busca ? 'Nenhum cliente encontrado' : 'Nenhum cliente cadastrado'}
          description={
            filters.busca
              ? 'Tente outro nome ou documento.'
              : 'Cadastre o primeiro cliente para começar a vincular processos a ele.'
          }
          action={
            filters.busca ? null : (
              <Button asChild>
                <Link href={`${routes.clients}/novo`}>
                  <Plus aria-hidden />
                  Cadastrar cliente
                </Link>
              </Button>
            )
          }
        />
      )}
    </div>
  )
}
