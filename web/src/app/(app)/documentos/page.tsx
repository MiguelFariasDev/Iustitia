import type { Metadata } from 'next'
import { FolderOpen } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Documentos' }

/** Sem backend ainda: a página não exibe dados de exemplo, só o que o módulo vai entregar. */
export default function Page() {
  return (
    <ComingSoon
      title="Documentos"
      description="Central de arquivos do escritório: peças, procurações, contratos e comprovantes."
      icon={FolderOpen}
      phase="Fase 4"
      highlights={[
    'Upload e organização de arquivos por cliente e processo',
    'Modelos de procuração e contrato',
    'Busca no conteúdo dos documentos',
  ]}
    />
  )
}
