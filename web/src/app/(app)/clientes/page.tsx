import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { ClientesView } from '@/features/clientes/components/ClientesView'

export const metadata: Metadata = { title: 'Clientes' }

/**
 * A listagem não faz prefetch no servidor: os filtros são estado do cliente e a primeira
 * consulta depende deles. Pré-buscar a lista sem filtro só para descartá-la no primeiro
 * clique gastaria uma ida ao banco por visita.
 */
export default function ClientesPage() {
  return (
    <>
      <PageHeader
        title="Clientes"
        description="Pessoas e empresas atendidas pelo escritório. Todo processo é cadastrado sob um cliente."
      />
      <ClientesView />
    </>
  )
}
