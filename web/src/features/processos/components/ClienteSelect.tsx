'use client'

import { useQuery } from '@tanstack/react-query'
import Link from 'next/link'
import { clientesQuery } from '@/features/clientes/queries'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { routes } from '@/config/routes'

interface ClienteSelectProps {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
}

/**
 * Seleção do cliente do processo.
 *
 * Lista só os ATIVOS: cadastrar um processo novo sob um cliente desativado é quase sempre
 * engano, e o backend aceitaria em silêncio. Um cliente desativado que ainda precise de
 * processo novo deve ser reativado primeiro — a decisão fica explícita.
 *
 * pageSize alto e sem paginação: um escritório pequeno tem dezenas de clientes, e um
 * <select> é a interface certa nessa escala. Se a carteira crescer, isto vira um combobox
 * com busca no servidor.
 */
export function ClienteSelect({ value, onChange, disabled }: ClienteSelectProps) {
  const { data, isPending } = useQuery(clientesQuery({ apenasAtivos: true, pageSize: 100 }))

  if (!isPending && data && data.items.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nenhum cliente ativo.{' '}
        <Link
          href={`${routes.clients}/novo`}
          className="font-medium text-secondary underline-offset-4 hover:underline"
        >
          Cadastre um cliente
        </Link>{' '}
        antes de criar o processo.
      </p>
    )
  }

  return (
    <Select value={value} onValueChange={onChange} disabled={disabled || isPending}>
      <SelectTrigger aria-label="Cliente do processo">
        <SelectValue placeholder={isPending ? 'Carregando clientes…' : 'Selecione o cliente'} />
      </SelectTrigger>
      <SelectContent>
        {data?.items.map((cliente) => (
          <SelectItem key={cliente.id} value={cliente.id}>
            {cliente.nome} · {cliente.documento}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}
