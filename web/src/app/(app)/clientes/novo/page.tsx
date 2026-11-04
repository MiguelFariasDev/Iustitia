import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { NovoClienteForm } from '@/features/clientes/components/NovoClienteForm'

export const metadata: Metadata = { title: 'Novo cliente' }

export default function NovoClientePage() {
  return (
    <>
      <PageHeader
        title="Novo cliente"
        description="Cadastre a pessoa ou empresa. Só o tipo, o nome e o documento são obrigatórios."
      />
      <NovoClienteForm />
    </>
  )
}
