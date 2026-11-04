'use client'

import { routes } from '@/config/routes'
import { useCreateCliente } from '../hooks'
import { ClienteForm } from './ClienteForm'

export function NovoClienteForm() {
  const { mutate, isPending } = useCreateCliente()

  return (
    <ClienteForm
      mode="create"
      submitLabel="Cadastrar cliente"
      isSubmitting={isPending}
      onSubmit={(values) => mutate(values)}
      cancelHref={routes.clients}
    />
  )
}
