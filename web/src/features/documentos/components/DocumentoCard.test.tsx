import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import type { Documento } from '../types'
import { DocumentoCard } from './DocumentoCard'

const documento: Documento = {
  id: 'doc-1',
  nome: 'Petição inicial.pdf',
  tipoArquivo: 'PDF',
  tamanhoBytes: 482_000,
  criadoEm: '2026-10-20T12:00:00.000Z',
  vinculo: { tipo: 'Processo', rotulo: '5001001-10.2026.8.26.0001' },
}

describe('DocumentoCard', () => {
  it('mostra nome, tipo, tamanho, data e vínculo', () => {
    renderWithProviders(
      <DocumentoCard documento={documento} onDownload={vi.fn()} onDelete={vi.fn()} />
    )

    expect(screen.getByRole('heading', { name: 'Petição inicial.pdf' })).toBeInTheDocument()
    expect(screen.getByText('PDF · 471 KB')).toBeInTheDocument()
    expect(screen.getByText('20/10/2026')).toBeInTheDocument()
    expect(screen.getByText('Processo')).toBeInTheDocument()
    expect(screen.getByText('5001001-10.2026.8.26.0001')).toBeInTheDocument()
  })

  it('documento avulso não mostra referência', () => {
    renderWithProviders(
      <DocumentoCard
        documento={{ ...documento, vinculo: { tipo: 'Avulso', rotulo: null } }}
        onDownload={vi.fn()}
        onDelete={vi.fn()}
      />
    )

    expect(screen.getByText('Avulso')).toBeInTheDocument()
    expect(screen.queryByText('5001001-10.2026.8.26.0001')).not.toBeInTheDocument()
  })

  it('baixar e excluir chamam os callbacks com o documento', async () => {
    const onDownload = vi.fn()
    const onDelete = vi.fn()
    const { user } = renderWithProviders(
      <DocumentoCard documento={documento} onDownload={onDownload} onDelete={onDelete} />
    )

    await user.click(screen.getByRole('button', { name: 'Baixar Petição inicial.pdf' }))
    await user.click(screen.getByRole('button', { name: 'Excluir Petição inicial.pdf' }))

    expect(onDownload).toHaveBeenCalledWith(documento)
    expect(onDelete).toHaveBeenCalledWith(documento)
  })
})
