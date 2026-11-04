import type { Metadata } from 'next'
import { PageHeader } from '@/components/shared/PageHeader'
import { DocumentoUploadModal } from '@/features/documentos/components/DocumentoUploadModal'
import { DocumentosView } from '@/features/documentos/components/DocumentosView'

export const metadata: Metadata = { title: 'Documentos' }

export default function DocumentosPage() {
  return (
    <>
      <PageHeader
        title="Documentos"
        description="Central de arquivos do escritório: peças, procurações, contratos e comprovantes."
        actions={<DocumentoUploadModal />}
      />
      <DocumentosView />
    </>
  )
}
