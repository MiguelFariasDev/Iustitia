'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { clientesQuery } from '@/features/clientes/queries'
import { handleApiError } from '@/lib/api/toastHandler'
import { criarCaso } from '../api'
import { casosQuery } from '../queries'
import type { AreaPeca, Caso } from '../types'

/**
 * Escolha (ou abertura) do caso. É o limite do que a IA pode ler: os anexos de um caso nunca entram
 * na peça de outro.
 */
export function CasoPeca({
  casoId,
  area,
  onEscolher,
  disabled,
}: {
  casoId: string
  area: AreaPeca
  onEscolher: (caso: Caso) => void
  disabled?: boolean
}) {
  const queryClient = useQueryClient()
  const casos = useQuery(casosQuery())
  const [criando, setCriando] = useState(false)
  const [titulo, setTitulo] = useState('')
  const [clienteId, setClienteId] = useState('')
  const clientes = useQuery({
    ...clientesQuery({ pageSize: 100, apenasAtivos: true }),
    enabled: criando,
  })
  const abertos = (casos.data ?? []).filter((caso) => caso.status === 'Aberto')

  const criar = useMutation({
    mutationFn: () =>
      criarCaso({
        clienteId,
        titulo: titulo.trim(),
        area,
        clienteNome: clientes.data?.items.find((c) => c.id === clienteId)?.nome,
      }),
    onSuccess: async (caso) => {
      await queryClient.invalidateQueries({ queryKey: ['casos', 'list'] })
      setCriando(false)
      setTitulo('')
      setClienteId('')
      onEscolher(caso)
    },
    onError: (error) => handleApiError(error),
  })

  return (
    <section aria-labelledby="caso-titulo" className="space-y-3">
      <div className="space-y-1">
        <h2 id="caso-titulo" className="text-sm font-medium">
          Caso
        </h2>
        <p className="text-xs text-muted-foreground">
          Obrigatório. A IA só lê os documentos deste caso; nada de outro cliente entra na peça.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={casoId}
          disabled={disabled}
          onValueChange={(id) => {
            const caso = abertos.find((c) => c.id === id)
            if (caso) onEscolher(caso)
          }}
        >
          <SelectTrigger aria-labelledby="caso-titulo" className="w-full sm:w-96">
            <SelectValue
              placeholder={abertos.length === 0 ? 'Nenhum caso aberto' : 'Selecione o caso'}
            />
          </SelectTrigger>
          <SelectContent>
            {abertos.map((caso) => (
              <SelectItem key={caso.id} value={caso.id}>
                {caso.titulo}
                {caso.clienteNome ? ` — ${caso.clienteNome}` : ''}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button
          type="button"
          variant="outline"
          disabled={disabled}
          aria-expanded={criando}
          onClick={() => setCriando((aberto) => !aberto)}
        >
          <Plus aria-hidden />
          Novo caso
        </Button>
      </div>

      {criando ? (
        <div className="grid gap-3 rounded-lg border p-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <div className="space-y-1.5">
            <label htmlFor="novo-caso-titulo" className="text-xs font-medium">
              Título do caso
            </label>
            <Input
              id="novo-caso-titulo"
              value={titulo}
              maxLength={200}
              onChange={(evento) => setTitulo(evento.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <label id="novo-caso-cliente" className="text-xs font-medium">
              Cliente
            </label>
            <Select value={clienteId} onValueChange={setClienteId}>
              <SelectTrigger aria-labelledby="novo-caso-cliente" className="w-full">
                <SelectValue placeholder="Selecione o cliente" />
              </SelectTrigger>
              <SelectContent>
                {(clientes.data?.items ?? []).map((cliente) => (
                  <SelectItem key={cliente.id} value={cliente.id}>
                    {cliente.nome}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button
            type="button"
            disabled={titulo.trim() === '' || clienteId === '' || criar.isPending}
            onClick={() => criar.mutate()}
          >
            {criar.isPending ? <LoadingSpinner size="sm" decorative /> : null}
            Abrir caso
          </Button>
        </div>
      ) : null}
    </section>
  )
}
