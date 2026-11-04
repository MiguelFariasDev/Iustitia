'use client'

import { routes } from '@/config/routes'
import { useCreateContato } from '../hooks'
import { ContatoForm } from './ContatoForm'

export function NovoContatoForm() {
  const { mutate, isPending } = useCreateContato()

  return (
    <ContatoForm
      submitLabel="Salvar"
      isSubmitting={isPending}
      onSubmit={(values) => mutate(values)}
      cancelHref={routes.contacts}
    />
  )
}
