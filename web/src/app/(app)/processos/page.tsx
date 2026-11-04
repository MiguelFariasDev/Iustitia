import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { ProcessosView } from '@/features/processos/components/ProcessosView'

export const metadata: Metadata = { title: 'Processos' }

export default function ProcessosPage() {
  return (
    <>
      <PageHeader
        title="Processos"
        description="Os processos que o escritório acompanha. Cadastrar um processo liga a busca automática das publicações dele no diário oficial."
      />
      <ProcessosView />
    </>
  )
}
