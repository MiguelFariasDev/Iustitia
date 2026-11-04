import type { Metadata } from 'next'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { AlertaDetail } from '@/features/alertas/components/AlertaDetail'

export const metadata: Metadata = { title: 'Alerta' }

export default async function AlertaDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Alerta"
        actions={
          <Button variant="ghost" size="sm" asChild>
            <Link href={routes.alerts}>Voltar para alertas</Link>
          </Button>
        }
      />
      <AlertaDetail alertaId={id} />
    </>
  )
}
