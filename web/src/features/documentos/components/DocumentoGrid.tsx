import type { Documento } from '../types'
import { DocumentoCard } from './DocumentoCard'

interface DocumentoGridProps {
  documentos: Documento[]
  onDownload: (documento: Documento) => void
  onDelete: (documento: Documento) => void
  /** Nome acessível da lista (ex.: "Documentos do escritório"). */
  label?: string
}

/** Lista de cards. É uma `<ul>` de verdade: leitores de tela anunciam "lista, N itens". */
export function DocumentoGrid({
  documentos,
  onDownload,
  onDelete,
  label = 'Documentos do escritório',
}: DocumentoGridProps) {
  return (
    <ul aria-label={label} className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      {documentos.map((documento) => (
        <li key={documento.id}>
          <DocumentoCard documento={documento} onDownload={onDownload} onDelete={onDelete} />
        </li>
      ))}
    </ul>
  )
}
