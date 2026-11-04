import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { EditarProcessoForm } from '@/features/processos/components/EditarProcessoForm'

export const metadata: Metadata = { title: 'Editar processo' }

export default async function EditarProcessoPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Editar processo"
        description="O número CNJ não pode ser alterado — é por ele que as publicações estão vinculadas."
      />
      <EditarProcessoForm processoId={id} />
    </>
  )
}
