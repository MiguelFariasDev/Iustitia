import type { Metadata } from 'next'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { ClienteDetail } from '@/features/clientes/components/ClienteDetail'

export const metadata: Metadata = { title: 'Cliente' }

export default async function ClienteDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Cliente"
        description="Dados cadastrais e situação."
        actions={
          <Button variant="ghost" size="sm" asChild>
            <Link href={routes.clients}>Voltar para clientes</Link>
          </Button>
        }
      />
      <ClienteDetail clienteId={id} />
    </>
  )
}
