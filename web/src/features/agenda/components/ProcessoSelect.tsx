'use client'

import { useQuery } from '@tanstack/react-query'
import { processosQuery } from '@/features/processos/queries'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

/** Valor reservado: o Select do Radix não aceita `value=""` em item. */
const NENHUM = '__nenhum__'

interface ProcessoSelectProps {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
}

/**
 * Vínculo opcional do evento a um processo. Lista os primeiros 100 processos: um escritório
 * de até 15 advogados cabe nisso; acima disso vira combobox com busca no servidor.
 */
export function ProcessoSelect({ value, onChange, disabled }: ProcessoSelectProps) {
  const { data } = useQuery(processosQuery({ pageSize: 100, status: 'Ativo' }))

  return (
    <Select
      value={value === '' ? NENHUM : value}
      onValueChange={(next) => onChange(next === NENHUM ? '' : next)}
      disabled={disabled}
    >
      <SelectTrigger aria-label="Processo vinculado">
        <SelectValue placeholder="Sem processo" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NENHUM}>Sem processo</SelectItem>
        {data?.items.map((processo) => (
          <SelectItem key={processo.id} value={processo.id}>
            {processo.cnjNumber}
            {processo.parteContraria ? ` — ${processo.parteContraria}` : ''}
          </SelectItem>
        ))}
        {/* Processo já vinculado que não está nos 100 ativos: mantém o valor selecionável. */}
        {value !== '' && !data?.items.some((p) => p.id === value) ? (
          <SelectItem value={value}>Processo vinculado</SelectItem>
        ) : null}
      </SelectContent>
    </Select>
  )
}
