import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { NovoContatoForm } from '@/features/contatos/components/NovoContatoForm'

export const metadata: Metadata = { title: 'Novo contato' }

export default function NovoContatoPage() {
  return (
    <>
      <PageHeader
        title="Novo contato"
        description="Só o nome é obrigatório. Documento, e-mail e telefone ajudam a localizar a pessoa depois."
      />
      <NovoContatoForm />
    </>
  )
}
