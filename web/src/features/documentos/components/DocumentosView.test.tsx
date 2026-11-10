import { screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { mockListDocumentos, resetDocumentosMock } from '../mocks'
import { DocumentosView } from './DocumentosView'

beforeEach(() => resetDocumentosMock())

describe('DocumentosView', () => {
  it('filtra por tipo e por vínculo em controles segmentados, ambos começando em "Todos"', async () => {
    const { user } = renderWithProviders(<DocumentosView />)
    const total = mockListDocumentos({}).length
    await waitFor(() =>
      expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(total)
    )

    const tipo = screen.getByRole('radiogroup', { name: 'Filtrar por tipo de arquivo' })
    const vinculo = screen.getByRole('radiogroup', { name: 'Filtrar por vínculo' })
    expect(within(tipo).getByRole('radio', { name: 'Todos' })).toBeChecked()
    expect(within(vinculo).getByRole('radio', { name: 'Todos' })).toBeChecked()

    await user.click(within(tipo).getByRole('radio', { name: 'PDF' }))

    const pdfs = mockListDocumentos({ tipoArquivo: 'PDF' }).length
    await waitFor(() =>
      expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(pdfs)
    )

    await user.click(within(tipo).getByRole('radio', { name: 'Todos' }))
    await waitFor(() =>
      expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(total)
    )
  })
})
