import type { Metadata } from 'next'
import { FolderOpen } from 'lucide-react'
import { ComingSoon } from '@/components/shared/ComingSoon'

export const metadata: Metadata = { title: 'Documentos' }

export default function DocumentosPage() {
  return (
    <ComingSoon
      title="Documentos"
      description="Repositório de arquivos do escritório, por processo e por cliente."
      icon={FolderOpen}
      phase="Fase 4"
      highlights={[
        'Upload com organização por processo, cliente e tipo',
        'Busca por conteúdo com reconhecimento de texto',
        'Controle de versões e permissões por papel',
      ]}
    />
  )
}
