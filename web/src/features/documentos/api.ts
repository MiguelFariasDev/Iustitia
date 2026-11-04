import { ApiError } from '@/lib/api/errors'
import { mockResponse } from '@/lib/api/mock'
import { downloadBlob } from '@/lib/download'
import {
  mockDeleteDocumento,
  mockGetArquivo,
  mockListDocumentos,
  mockUploadDocumento,
} from './mocks'
import type { UploadFormValues } from './schemas'
import type { Documento, DocumentosFilters } from './types'

/**
 * Documentos ainda não têm endpoint nem storage: tudo usa o repositório em memória.
 *
 * TODO(documentos-backend): upload multipart para `/documentos`, listagem paginada e download
 * por URL assinada. Quando existir, remover o aviso de demonstração da tela.
 */
export function listDocumentos(filters: DocumentosFilters = {}): Promise<Documento[]> {
  return mockResponse(mockListDocumentos(filters), 250)
}

// `structuredClone` (em mockResponse) não clona `File` de forma útil: devolvemos só os metadados.
export function uploadDocumento(values: UploadFormValues): Promise<Documento> {
  return mockResponse(mockUploadDocumento(values), 300)
}

export async function deleteDocumento(id: string): Promise<void> {
  if (!mockDeleteDocumento(id)) {
    throw new ApiError(404, {
      errorCode: 'DOCUMENT_NOT_FOUND',
      detail: 'Documento não encontrado.',
    })
  }

  await mockResponse(null, 150)
}

/**
 * Baixa o documento. Arquivo enviado nesta sessão: o conteúdo real. Documento de exemplo:
 * um .txt que diz, sem rodeio, que não há conteúdo — entregar um PDF vazio com o nome do
 * original passaria por um documento de verdade.
 */
export function baixarDocumento(documento: Documento): void {
  const arquivo = mockGetArquivo(documento.id)
  const blob =
    arquivo ??
    new Blob(
      [`Documento de demonstração.\nO arquivo "${documento.nome}" não tem conteúdo real.\n`],
      { type: 'text/plain;charset=utf-8' }
    )
  const nome = arquivo ? documento.nome : `${documento.nome}.txt`

  downloadBlob(nome, blob)
}
