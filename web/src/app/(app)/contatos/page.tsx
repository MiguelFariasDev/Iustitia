import { Plus } from 'lucide-react'
import type { Metadata } from 'next'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { ContatoList } from '@/features/contatos/components/ContatoList'

export const metadata: Metadata = { title: 'Contatos' }

/** Sem prefetch: o filtro é estado do cliente (mesma razão da listagem de clientes). */
export default function ContatosPage() {
  return (
    <>
      <PageHeader
        title="Contatos"
        description="Partes contrárias, testemunhas, fornecedores e outras pessoas que não são clientes."
        actions={
          <Button asChild>
            <Link href={`${routes.contacts}/novo`}>
              <Plus aria-hidden />
              Novo contato
            </Link>
          </Button>
        }
      />
      <ContatoList />
    </>
  )
}
