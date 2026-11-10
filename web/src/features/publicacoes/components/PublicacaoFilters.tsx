'use client'

import { useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { SearchInput } from '@/components/shared/SearchInput'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { clientesQuery } from '@/features/clientes/queries'
import { cn } from '@/lib/utils'
import { countActiveFilters } from '../filters'
import { statusMeta } from '../presentation'
import { tribunais } from '../tribunais'
import { publicationStatuses, type PublicationsFilters } from '../types'

const TODOS = '__todos__'

/** Situações que NÃO são visão rápida: descartadas e arquivadas. */
const situacoesExtras = publicationStatuses.filter(
  (status) => status !== 'untreated' && status !== 'treated'
)

interface PublicacaoFiltersProps {
  filters: PublicationsFilters
  /** Qualquer mudança de filtro volta para a página 1 (feito por quem usa). */
  onChange: (patch: Partial<PublicationsFilters>) => void
  onClear: () => void
}

export function PublicacaoFilters({ filters, onChange, onClear }: PublicacaoFiltersProps) {
  const { data: clientes } = useQuery(clientesQuery({ pageSize: 100 }))
  const active = countActiveFilters(filters)

  return (
    <div className="space-y-3" role="search" aria-label="Filtrar publicações">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center">
        <SearchInput
          // `key` força o campo a recomeçar vazio quando os filtros são limpos de fora.
          key={filters.busca ?? ''}
          value={filters.busca ?? ''}
          onSearch={(busca) => onChange({ busca: busca || null })}
          label="Pesquisar publicações"
          placeholder="Processo ou cliente…"
          className="lg:max-w-sm"
        />

        <div className="flex flex-wrap items-center gap-2">
          <Select
            value={filters.tribunal ?? TODOS}
            onValueChange={(value) => onChange({ tribunal: value === TODOS ? null : value })}
          >
            <SelectTrigger className="w-36" aria-label="Filtrar por tribunal">
              <SelectValue placeholder="Tribunal" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TODOS}>Todos os tribunais</SelectItem>
              {tribunais.map((sigla) => (
                <SelectItem key={sigla} value={sigla}>
                  {sigla}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={filters.clienteId ?? TODOS}
            onValueChange={(value) => onChange({ clienteId: value === TODOS ? null : value })}
          >
            <SelectTrigger className="w-48" aria-label="Filtrar por cliente">
              <SelectValue placeholder="Cliente" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TODOS}>Todos os clientes</SelectItem>
              {clientes?.items.map((cliente) => (
                <SelectItem key={cliente.id} value={cliente.id}>
                  {cliente.nome}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="flex items-center gap-1.5">
            <Label htmlFor="pub-data-inicio" className="sr-only">
              Divulgado a partir de
            </Label>
            <Input
              id="pub-data-inicio"
              type="date"
              name="dataInicio"
              autoComplete="off"
              value={filters.from ?? ''}
              max={filters.to ?? undefined}
              onChange={(e) => onChange({ from: e.target.value || null })}
              className="w-36"
            />
            <span aria-hidden className="text-muted-foreground">
              –
            </span>
            <Label htmlFor="pub-data-fim" className="sr-only">
              Divulgado até
            </Label>
            <Input
              id="pub-data-fim"
              type="date"
              name="dataFim"
              autoComplete="off"
              value={filters.to ?? ''}
              min={filters.from ?? undefined}
              onChange={(e) => onChange({ to: e.target.value || null })}
              className="w-36"
            />
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <div
          className="flex flex-wrap items-center gap-2"
          role="group"
          aria-label="Filtrar por status"
        >
          {/* Aguardando revisão e Tratadas são as visões rápidas do topo (SegmentedControl). */}
          {situacoesExtras.map((value) => (
            <Chip
              key={value}
              active={filters.status === value}
              onClick={() => onChange({ status: filters.status === value ? null : value })}
            >
              {statusMeta[value].label}
            </Chip>
          ))}
        </div>

        <span aria-hidden className="mx-1 h-4 w-px bg-border" />

        <div
          className="flex flex-wrap items-center gap-2"
          role="group"
          aria-label="Filtrar por leitura"
        >
          {/* "Não lidas" é uma visão rápida do topo; aqui ficam só as lidas. */}
          <Chip
            active={filters.isRead === true}
            onClick={() => onChange({ isRead: filters.isRead === true ? null : true })}
          >
            Lidas
          </Chip>
        </div>

        {active > 0 ? (
          <Button variant="ghost" size="sm" onClick={onClear} className="ml-auto">
            <X aria-hidden />
            Limpar filtros ({active})
          </Button>
        ) : null}
      </div>
    </div>
  )
}

function Chip({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
        active
          ? 'border-secondary bg-secondary text-secondary-foreground'
          : 'border-border text-muted-foreground hover:bg-accent hover:text-foreground'
      )}
    >
      {children}
      {active ? <X className="size-3" aria-hidden /> : null}
    </button>
  )
}
