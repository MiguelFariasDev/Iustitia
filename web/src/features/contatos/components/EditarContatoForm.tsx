'use client'

import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { routes } from '@/config/routes'
import { useContato, useUpdateContato } from '../hooks'
import type { ContatoFormValues } from '../schemas'
import type { Contato } from '../types'
import { ContatoForm } from './ContatoForm'

export function EditarContatoForm({ contatoId }: { contatoId: string }) {
  const { data: contato, isPending } = useContato(contatoId)
  const { mutate, isPending: isSubmitting } = useUpdateContato(contatoId)

  if (isPending || !contato) {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Carregando contato…" />
      </div>
    )
  }

  return (
    <ContatoForm
      defaultValues={toFormValues(contato)}
      submitLabel="Salvar alterações"
      isSubmitting={isSubmitting}
      onSubmit={(values) => mutate(values)}
      cancelHref={routes.contacts}
    />
  )
}

/** `null` vira string vazia: inputs controlados não podem receber `null`. */
function toFormValues(contato: Contato): ContatoFormValues {
  return {
    nome: contato.nome,
    tipo: contato.tipo,
    documento: contato.documento ?? '',
    email: contato.email ?? '',
    telefone: contato.telefone ?? '',
    observacoes: contato.observacoes ?? '',
  }
}
