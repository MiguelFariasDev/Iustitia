import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { IndicadoresView } from './IndicadoresView'

describe('IndicadoresView', () => {
  it('o período é um controle segmentado que começa em "Mês atual" e muda a descrição', async () => {
    const { user } = renderWithProviders(<IndicadoresView />)
    await screen.findByText('Total de processos ativos')

    expect(screen.getByRole('radio', { name: 'Mês atual' })).toBeChecked()
    expect(screen.getByText(/Publicações capturadas — mês atual/)).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'Ano' }))

    await waitFor(() => expect(screen.getByRole('radio', { name: 'Ano' })).toBeChecked())
    await waitFor(() =>
      expect(screen.getByText(/Publicações capturadas — ano/)).toBeInTheDocument()
    )
  })
})
