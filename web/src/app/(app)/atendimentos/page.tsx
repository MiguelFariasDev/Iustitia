import type { Metadata } from 'next'
import { Suspense } from 'react'
import { PageHeader } from '@/components/shared/PageHeader'
import { Skeleton } from '@/components/ui/skeleton'
import { AtendimentosView } from '@/features/atendimentos/components/AtendimentosView'

export const metadata: Metadata = { title: 'Atendimentos' }

export default function AtendimentosPage() {
  return (
    <>
      <PageHeader
        title="Atendimentos"
        description="Conversas de WhatsApp com os clientes: acompanhe o bot, assuma quando precisarem de você e responda daqui."
      />
      {/* useSearchParams (conversa aberta vinda da URL) exige uma fronteira de Suspense. */}
      <Suspense
        fallback={
          <Skeleton
            className="h-96 rounded-xl"
            role="status"
            aria-label="Carregando atendimentos"
          />
        }
      >
        <AtendimentosView />
      </Suspense>
    </>
  )
}
