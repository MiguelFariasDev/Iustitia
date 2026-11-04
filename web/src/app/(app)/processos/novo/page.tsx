import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { NovoProcessoForm } from '@/features/processos/components/NovoProcessoForm'

export const metadata: Metadata = { title: 'Novo processo' }

export default function NovoProcessoPage() {
  return (
    <>
      <PageHeader
        title="Novo processo"
        description="Informe o número CNJ e o cliente. A partir do cadastro, as publicações deste processo passam a ser buscadas no diário oficial."
      />
      <NovoProcessoForm />
    </>
  )
}
