'use client'

import { X } from 'lucide-react'
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
import { cn } from '@/lib/utils'
import { alertTypes, type AlertasFilters, type AlertaTab } from '../types'
import { alertTypeMeta } from './AlertaCard'

const TODOS = '__todos__'

interface AlertaFiltersProps {
  tab: AlertaTab
  filters: AlertasFilters
  onChange: (patch: Partial<AlertasFilters>) => void
  onClear: () => void
}

/**
 * Tipo e leitura só aparecem na aba "Todos": nas outras, a aba JÁ define esses dois filtros, e
 * oferecê-los de novo deixaria escolher combinações que se contradizem (ex.: "Importantes"
 * — não lidos — com "lidas"). O período vale em qualquer aba.
 */
export function AlertaFilters({ tab, filters, onChange, onClear }: AlertaFiltersProps) {
  const showTypeAndRead = tab === 'all'
  const active = [
    showTypeAndRead && filters.types?.length ? 1 : 0,
    showTypeAndRead && filters.isRead !== null && filters.isRead !== undefined ? 1 : 0,
    filters.from ? 1 : 0,
    filters.to ? 1 : 0,
  ].reduce<number>((sum, n) => sum + n, 0)

  return (
    <div className="flex flex-wrap items-center gap-2" role="search" aria-label="Filtrar alertas">
      {showTypeAndRead ? (
        <>
          <Select
            value={filters.types?.[0] ?? TODOS}
            onValueChange={(value) =>
              onChange({ types: value === TODOS ? [] : [value as (typeof alertTypes)[number]] })
            }
          >
            <SelectTrigger className="w-40" aria-label="Filtrar por tipo">
              <SelectValue placeholder="Tipo" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={TODOS}>Todos os tipos</SelectItem>
              {alertTypes.map((type) => (
                <SelectItem key={type} value={type}>
                  {alertTypeMeta[type].label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <div className="flex items-center gap-2" role="group" aria-label="Filtrar por leitura">
            {([false, true] as const).map((value) => (
              <button
                key={String(value)}
                type="button"
                aria-pressed={filters.isRead === value}
                onClick={() => onChange({ isRead: filters.isRead === value ? null : value })}
                className={cn(
                  'inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                  filters.isRead === value
                    ? 'border-secondary bg-secondary text-secondary-foreground'
                    : 'border-border text-muted-foreground hover:bg-accent hover:text-foreground'
                )}
              >
                {value ? 'Lidos' : 'Não lidos'}
              </button>
            ))}
          </div>
        </>
      ) : null}

      <div className="flex items-center gap-1.5">
        <Label htmlFor="alerta-data-inicio" className="sr-only">
          Recebidos a partir de
        </Label>
        <Input
          id="alerta-data-inicio"
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
        <Label htmlFor="alerta-data-fim" className="sr-only">
          Recebidos até
        </Label>
        <Input
          id="alerta-data-fim"
          type="date"
          name="dataFim"
          autoComplete="off"
          value={filters.to ?? ''}
          min={filters.from ?? undefined}
          onChange={(e) => onChange({ to: e.target.value || null })}
          className="w-36"
        />
      </div>

      {active > 0 ? (
        <Button variant="ghost" size="sm" onClick={onClear}>
          <X aria-hidden />
          Limpar filtros ({active})
        </Button>
      ) : null}
    </div>
  )
}
