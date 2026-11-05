'use client'

import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ErrorState } from '@/components/shared/ErrorState'
import { SearchInput } from '@/components/shared/SearchInput'
import { Button } from '@/components/ui/button'
import { clientesQuery } from '@/features/clientes/queries'
import { useLinkCliente } from '../hooks'

/** Busca um cliente do escritório e vincula a conversa a ele. */
export function ClienteLinker({ atendimentoId }: { atendimentoId: string }) {
  const [busca, setBusca] = useState('')
  const link = useLinkCliente(atendimentoId)
  const results = useQuery({
    ...clientesQuery({ busca, apenasAtivos: true, pageSize: 5 }),
    enabled: busca.length >= 2,
  })

  return (
    <div className="space-y-2">
      <SearchInput
        value={busca}
        onSearch={setBusca}
        label="Buscar cliente para vincular"
        placeholder="Nome ou CPF/CNPJ"
        className="sm:max-w-none"
      />

      {busca.length < 2 ? (
        <p className="text-xs text-muted-foreground">Digite ao menos 2 letras para buscar.</p>
      ) : results.isError ? (
        <ErrorState
          error={results.error}
          onRetry={() => void results.refetch()}
          title="Não foi possível buscar clientes"
        />
      ) : results.isPending ? (
        <p className="text-xs text-muted-foreground" role="status">
          Buscando…
        </p>
      ) : results.data.items.length === 0 ? (
        <p className="text-xs text-muted-foreground">Nenhum cliente encontrado.</p>
      ) : (
        <ul className="space-y-1">
          {results.data.items.map((cliente) => (
            <li key={cliente.id} className="flex items-center justify-between gap-2 text-sm">
              <span className="min-w-0">
                <span className="block truncate font-medium">{cliente.nome}</span>
                <span className="block truncate text-xs text-muted-foreground">
                  {cliente.documento}
                </span>
              </span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                disabled={link.isPending}
                onClick={() => link.mutate(cliente.id)}
                aria-label={`Vincular a ${cliente.nome}`}
              >
                Vincular
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
