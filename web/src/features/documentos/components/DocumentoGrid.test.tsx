import { screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { mockListDocumentos, resetDocumentosMock } from '../mocks'
import { DocumentoGrid } from './DocumentoGrid'

beforeEach(() => resetDocumentosMock())

describe('DocumentoGrid', () => {
  it('é uma lista nomeada com um item por documento', () => {
    const docs = mockListDocumentos()
    renderWithProviders(<DocumentoGrid documentos={docs} onDownload={vi.fn()} onDelete={vi.fn()} />)

    const lista = screen.getByRole('list', { name: 'Documentos do escritório' })
    expect(within(lista).getAllByRole('listitem')).toHaveLength(docs.length)
  })

  it('repassa download e exclusão do documento certo', async () => {
    const docs = mockListDocumentos()
    const onDownload = vi.fn()
    const onDelete = vi.fn()
    const { user } = renderWithProviders(
      <DocumentoGrid documentos={docs} onDownload={onDownload} onDelete={onDelete} />
    )

    await user.click(screen.getByRole('button', { name: 'Excluir Contrato de honorários.docx' }))
    await user.click(screen.getByRole('button', { name: 'Baixar Comprovante de pagamento.png' }))

    expect(onDelete).toHaveBeenCalledWith(expect.objectContaining({ id: 'doc-003' }))
    expect(onDownload).toHaveBeenCalledWith(expect.objectContaining({ id: 'doc-006' }))
  })
})

describe('mockListDocumentos', () => {
  it('mais recente primeiro e filtra por tipo, vínculo e nome', () => {
    const todos = mockListDocumentos()
    expect(todos[0]?.id).toBe('doc-001')

    expect(mockListDocumentos({ tipoArquivo: 'Imagem' }).map((d) => d.id)).toEqual([
      'doc-006',
      'doc-004',
    ])
    expect(mockListDocumentos({ vinculo: 'Avulso' })).toHaveLength(2)
    expect(mockListDocumentos({ busca: 'PROCURAÇÃO' }).map((d) => d.id)).toEqual(['doc-002'])
  })
})
