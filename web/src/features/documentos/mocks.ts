import { daysFromNow } from '@/mocks/dates'
import { detectarTipoArquivo } from './arquivo'
import type { UploadFormValues } from './schemas'
import type { Documento, DocumentosFilters } from './types'

/**
 * DADOS DE DEMONSTRAÇÃO. O módulo Documentos não existe no backend (a pasta
 * `Documents.*` está vazia): este é um repositório EM MEMÓRIA. Os arquivos enviados ficam só
 * na memória da aba — recarregar a página os perde. Os documentos de exemplo não têm
 * conteúdo real.
 *
 * TODO(documentos-backend): trocar por upload multipart + URL assinada do storage (api.ts).
 */
function seed(): Documento[] {
  const doc = (
    id: string,
    nome: string,
    tamanhoBytes: number,
    dias: number,
    tipo: Documento['vinculo']['tipo'],
    rotulo: string | null
  ): Documento => ({
    id,
    nome,
    tipoArquivo: detectarTipoArquivo(nome),
    tamanhoBytes,
    criadoEm: daysFromNow(-dias),
    vinculo: { tipo, rotulo },
  })

  return [
    doc('doc-001', 'Petição inicial.pdf', 482_000, 3, 'Processo', '5001001-10.2026.8.26.0001'),
    doc(
      'doc-002',
      'Procuração — Maria Aparecida.pdf',
      156_000,
      9,
      'Cliente',
      'Maria Aparecida Silva'
    ),
    doc('doc-003', 'Contrato de honorários.docx', 98_000, 15, 'Cliente', 'Padaria Boa Massa LTDA'),
    doc(
      'doc-004',
      'Foto do local do acidente.jpg',
      2_300_000,
      21,
      'Processo',
      '5001001-10.2026.8.26.0001'
    ),
    doc('doc-005', 'Modelo de notificação extrajudicial.docx', 41_000, 60, 'Avulso', null),
    doc('doc-006', 'Comprovante de pagamento.png', 310_000, 7, 'Avulso', null),
  ]
}

let store: Documento[] = seed()
let arquivos = new Map<string, File>()
let sequence = 100

export function resetDocumentosMock() {
  store = seed()
  arquivos = new Map()
  sequence = 100
}

export function mockListDocumentos(filters: DocumentosFilters = {}): Documento[] {
  const busca = filters.busca?.trim().toLowerCase()

  return store
    .filter((d) => !filters.tipoArquivo || d.tipoArquivo === filters.tipoArquivo)
    .filter((d) => !filters.vinculo || d.vinculo.tipo === filters.vinculo)
    .filter((d) => !busca || d.nome.toLowerCase().includes(busca))
    .sort((a, b) => b.criadoEm.localeCompare(a.criadoEm))
}

export function mockUploadDocumento(values: UploadFormValues): Documento {
  sequence += 1
  const created: Documento = {
    id: `doc-${sequence}`,
    nome: values.file.name,
    tipoArquivo: detectarTipoArquivo(values.file.name),
    tamanhoBytes: values.file.size,
    criadoEm: new Date().toISOString(),
    vinculo: {
      tipo: values.vinculoTipo,
      rotulo: values.vinculoTipo === 'Avulso' ? null : values.vinculoRotulo.trim(),
    },
  }
  store = [created, ...store]
  arquivos.set(created.id, values.file)

  return created
}

export function mockDeleteDocumento(id: string): boolean {
  const existed = store.some((d) => d.id === id)
  store = store.filter((d) => d.id !== id)
  arquivos.delete(id)

  return existed
}

/** O arquivo real, só para o que foi enviado nesta sessão; os de exemplo devolvem `null`. */
export function mockGetArquivo(id: string): File | null {
  return arquivos.get(id) ?? null
}
