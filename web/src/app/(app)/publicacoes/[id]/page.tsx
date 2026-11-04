import type { Metadata } from 'next'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { PublicacaoDetail } from '@/features/publicacoes/components/PublicacaoDetail'

export const metadata: Metadata = { title: 'Publicação' }

export default async function PublicacaoDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params

  return (
    <>
      <PageHeader
        title="Publicação"
        description="Texto integral, vínculo com o processo e revisão."
        actions={
          <Button variant="ghost" size="sm" asChild>
            <Link href={routes.publications}>Voltar para publicações</Link>
          </Button>
        }
      />
      <PublicacaoDetail publicationId={id} />
    </>
  )
}
