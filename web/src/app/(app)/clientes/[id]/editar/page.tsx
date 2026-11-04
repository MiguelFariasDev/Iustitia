import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { EditarClienteForm } from '@/features/clientes/components/EditarClienteForm'

export const metadata: Metadata = { title: 'Editar cliente' }

export default async function EditarClientePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Editar cliente"
        description="Tipo e documento não podem ser alterados — são a identidade do cliente."
      />
      <EditarClienteForm clienteId={id} />
    </>
  )
}
