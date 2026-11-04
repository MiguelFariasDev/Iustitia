import type { Metadata } from 'next'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { ProcessoDetail } from '@/features/processos/components/ProcessoDetail'

export const metadata: Metadata = { title: 'Processo' }

export default async function ProcessoDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Processo"
        description="Dados do caso, foro e situação de acompanhamento."
        actions={
          <Button variant="ghost" size="sm" asChild>
            <Link href={routes.processes}>Voltar para processos</Link>
          </Button>
        }
      />
      <ProcessoDetail processoId={id} />
    </>
  )
}
