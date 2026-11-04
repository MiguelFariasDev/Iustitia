'use client'

import { useQuery } from '@tanstack/react-query'
import { BriefcaseBusiness, Plus } from 'lucide-react'
import Link from 'next/link'
import { useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Pagination } from '@/components/shared/Pagination'
import { SearchInput } from '@/components/shared/SearchInput'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { routes } from '@/config/routes'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { listUsers } from '../../settings/api'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { processosQuery } from '../queries'
import {
  areaDireitoLabels,
  areasDireito,
  statusProcesso,
  statusProcessoMeta,
  type AreaDireito,
  type ProcessosFilters,
  type StatusProcesso,
} from '../types'

const TODOS = '__todos__'

export function ProcessosView() {
  const searchParams = useSearchParams()

  // Filtro inicial vindo da URL, para os cartões do painel levarem à lista já filtrada.
  // Só o estado INICIAL: daqui em diante quem manda são os controles da tela, e reagir à
  // URL depois desfaria o filtro que a pessoa acabou de mudar.
  const [filters, setFilters] = useState<ProcessosFilters>(() => {
    const status = searchParams.get('status')
    const area = searchParams.get('area')

    return {
      page: 1,
      status: statusProcesso.includes(status as StatusProcesso) ? (status as StatusProcesso) : null,
      area: areasDireito.includes(area as AreaDireito) ? (area as AreaDireito) : null,
    }
  })

  const { data: usuarios } = useQuery({ queryKey: ['users', 'list'], queryFn: listUsers })

  const { data, isPending, isFetching } = useQuery({
    ...processosQuery(filters),
    placeholderData: (previous) => previous,
  })

  function updateFilters(patch: Partial<ProcessosFilters>) {
    setFilters((current) => ({ ...current, ...patch, page: 1 }))
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <SearchInput
          onSearch={(busca) => updateFilters({ busca })}
          label="Pesquisar processos"
          placeholder="Número CNJ ou parte contrária…"
          className="lg:max-w-sm"
        />

        <div className="flex flex-wrap items-center gap-2">
          <Select
            value={filters.status ?? TODOS}
            onValueChange={(value) =>
              updateFilters({ status: value === TODOS ? null : (value as StatusProcesso) })
            }
          >
            <SelectTrigger className="w-40" aria-label="Filtrar por situação">
              <SelectValue placeholder="Situação" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TODOS}>Todas as situações</SelectItem>
              {statusProcesso.map((value) => (
                <SelectItem key={value} value={value}>
                  {statusProcessoMeta[value].label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={filters.area ?? TODOS}
            onValueChange={(value) =>
              updateFilters({ area: value === TODOS ? null : (value as AreaDireito) })
            }
          >
            <SelectTrigger className="w-40" aria-label="Filtrar por área">
              <SelectValue placeholder="Área" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TODOS}>Todas as áreas</SelectItem>
              {areasDireito.map((area) => (
                <SelectItem key={area} value={area}>
                  {areaDireitoLabels[area]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button asChild size="sm" variant="outline">
            <Link href={`${routes.processes}/importar`}>Importar</Link>
          </Button>

          <Button asChild size="sm">
            <Link href={`${routes.processes}/novo`}>
              <Plus aria-hidden />
              Novo processo
            </Link>
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <Select
          value={filters.responsavelUserId ?? TODOS}
          onValueChange={(value) =>
            updateFilters({ responsavelUserId: value === TODOS ? null : value })
          }
        >
          <SelectTrigger className="w-48" aria-label="Filtrar por responsável">
            <SelectValue placeholder="Responsável" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={TODOS}>Todos os responsáveis</SelectItem>
            {(usuarios ?? []).map((u) => (
              <SelectItem key={u.id} value={u.id}>
                {u.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <div className="space-y-1">
          <Label htmlFor="filtro-tribunal" className="text-xs">
            Tribunal
          </Label>
          <Input
            id="filtro-tribunal"
            className="w-32 uppercase"
            placeholder="TJCE"
            maxLength={20}
            value={filters.tribunal ?? ''}
            onChange={(e) => updateFilters({ tribunal: e.target.value.toUpperCase() })}
          />
        </div>

        <div className="space-y-1">
          <Label htmlFor="filtro-de" className="text-xs">
            Distribuído de
          </Label>
          <Input
            id="filtro-de"
            type="date"
            value={filters.distribuicaoDe ?? ''}
            onChange={(e) => updateFilters({ distribuicaoDe: e.target.value })}
          />
        </div>

        <div className="space-y-1">
          <Label htmlFor="filtro-ate" className="text-xs">
            até
          </Label>
          <Input
            id="filtro-ate"
            type="date"
            value={filters.distribuicaoAte ?? ''}
            onChange={(e) => updateFilters({ distribuicaoAte: e.target.value })}
          />
        </div>
      </div>

      {isPending ? (
        <div className="flex justify-center py-12">
          <LoadingSpinner label="Carregando processos…" />
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
                  <TableHead>Processo</TableHead>
                  <TableHead className="hidden md:table-cell">Área</TableHead>
                  <TableHead className="hidden lg:table-cell">Parte contrária</TableHead>
                  <TableHead className="hidden lg:table-cell">Distribuição</TableHead>
                  <TableHead>Situação</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items.map((processo) => (
                  <TableRow key={processo.id}>
                    <TableCell>
                      <Link
                        href={`${routes.processes}/${processo.id}` as never}
                        className="font-mono text-sm font-medium text-secondary underline-offset-4 hover:underline"
                      >
                        {processo.cnjNumber}
                      </Link>
                      {processo.tribunal ? (
                        <p className="text-xs text-muted-foreground">{processo.tribunal}</p>
                      ) : null}
                    </TableCell>
                    <TableCell className="hidden text-sm md:table-cell">
                      {areaDireitoLabels[processo.area]}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground lg:table-cell">
                      {processo.parteContraria ?? '—'}
                    </TableCell>
                    <TableCell className="hidden text-sm text-muted-foreground lg:table-cell">
                      {processo.dataDistribuicao ? formatDate(processo.dataDistribuicao) : '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge tone={statusProcessoMeta[processo.status].tone}>
                        {statusProcessoMeta[processo.status].label}
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
          icon={BriefcaseBusiness}
          title={filters.busca ? 'Nenhum processo encontrado' : 'Nenhum processo cadastrado'}
          description={
            filters.busca
              ? 'Tente outro número ou nome de parte.'
              : 'Cadastre um processo para o sistema começar a buscar as publicações dele no diário oficial.'
          }
          action={
            filters.busca ? null : (
              <Button asChild>
                <Link href={`${routes.processes}/novo`}>
                  <Plus aria-hidden />
                  Cadastrar processo
                </Link>
              </Button>
            )
          }
        />
      )}
    </div>
  )
}
