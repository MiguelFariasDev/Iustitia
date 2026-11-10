'use client'

import { useQuery } from '@tanstack/react-query'
import { listUsers } from '@/features/settings/api'
import { queryKeys } from '@/lib/query/queryKeys'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

/** Valor reservado: o Select do Radix não aceita `value=""` em item. */
const NENHUM = '__nenhum__'

interface ResponsavelSelectProps {
  value: string
  onChange: (value: string) => void
  /** Texto da opção vazia ("Todos os responsáveis" no filtro, "Eu" no formulário). */
  emptyLabel: string
  label: string
  className?: string
}

/** Responsável de um evento (formulário) ou filtro por responsável (agenda). */
export function ResponsavelSelect({
  value,
  onChange,
  emptyLabel,
  label,
  className,
}: ResponsavelSelectProps) {
  const { data: usuarios } = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers })

  return (
    <Select
      value={value === '' ? NENHUM : value}
      onValueChange={(next) => onChange(next === NENHUM ? '' : next)}
    >
      <SelectTrigger aria-label={label} className={className}>
        <SelectValue placeholder={emptyLabel} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NENHUM}>{emptyLabel}</SelectItem>
        {(usuarios ?? []).map((usuario) => (
          <SelectItem key={usuario.id} value={usuario.id}>
            {usuario.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
