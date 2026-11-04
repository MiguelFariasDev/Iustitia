import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { EditarContatoForm } from '@/features/contatos/components/EditarContatoForm'

export const metadata: Metadata = { title: 'Editar contato' }

export default async function EditarContatoPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader title="Editar contato" description="Atualize os dados de contato." />
      <EditarContatoForm contatoId={id} />
    </>
  )
}
