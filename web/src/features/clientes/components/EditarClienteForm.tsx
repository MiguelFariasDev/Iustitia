'use client'

import { useQuery } from '@tanstack/react-query'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { routes } from '@/config/routes'
import { useUpdateCliente } from '../hooks'
import { clienteQuery } from '../queries'
import type { ClienteFormValues } from '../schemas'
import type { Cliente } from '../types'
import { ClienteForm } from './ClienteForm'

export function EditarClienteForm({ clienteId }: { clienteId: string }) {
  const { data: cliente, isPending } = useQuery(clienteQuery(clienteId))
  const { mutate, isPending: isSubmitting } = useUpdateCliente(clienteId)

  if (isPending || !cliente) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando cliente…" />
      </div>
    )
  }

  return (
    <ClienteForm
      mode="edit"
      defaultValues={toFormValues(cliente)}
      submitLabel="Salvar alterações"
      isSubmitting={isSubmitting}
      onSubmit={(values) => mutate(values)}
      cancelHref={`${routes.clients}/${clienteId}`}
    />
  )
}

/**
 * Converte o cliente para os valores do formulário. Todo campo nulo vira string vazia: os
 * inputs são controlados, e passar `null` faria o React alternar entre controlado e não
 * controlado a cada render.
 */
function toFormValues(cliente: Cliente): ClienteFormValues {
  return {
    tipo: cliente.tipo,
    nome: cliente.nome,
    documento: cliente.documento,
    email: cliente.email ?? '',
    telefone: cliente.telefone ?? '',
    dataNascimento: cliente.dataNascimento?.slice(0, 10) ?? '',
    cep: cliente.endereco.cep ?? '',
    logradouro: cliente.endereco.logradouro ?? '',
    numero: cliente.endereco.numero ?? '',
    complemento: cliente.endereco.complemento ?? '',
    bairro: cliente.endereco.bairro ?? '',
    cidade: cliente.endereco.cidade ?? '',
    uf: cliente.endereco.uf ?? '',
    observacoes: cliente.observacoes ?? '',
  }
}
