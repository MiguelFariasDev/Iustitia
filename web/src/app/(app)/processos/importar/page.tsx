import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { ProcessoImportWizard } from '@/features/processos/components/ProcessoImportWizard'

export const metadata: Metadata = { title: 'Importar processos' }

export default function ImportarProcessosPage() {
  return (
    <>
      <PageHeader
        title="Importar processos"
        description="Cadastre vários processos de uma vez a partir de uma planilha CSV. Cada processo cadastrado passa a ter as publicações capturadas."
      />
      <ProcessoImportWizard />
    </>
  )
}
