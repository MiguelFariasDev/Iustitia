import { screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { resetContatosMock } from '../mocks'
import { ContatoList } from './ContatoList'

beforeEach(() => resetContatosMock())

async function renderList() {
  const utils = renderWithProviders(<ContatoList />)
  await screen.findByText('Carlos Eduardo Pereira')
  return utils
}

describe('ContatoList', () => {
  it('avisa que os dados são de demonstração', async () => {
    await renderList()

    expect(screen.getByRole('note')).toHaveTextContent('Dados de demonstração')
  })

  it('lista os contatos com tipo, documento, e-mail e telefone', async () => {
    await renderList()

    const linha = screen.getByRole('row', { name: /Carlos Eduardo Pereira/ })
    expect(within(linha).getByText('Parte contrária')).toBeInTheDocument()
    expect(within(linha).getByText('529.982.247-25')).toBeInTheDocument()
    expect(within(linha).getByText('carlos.pereira@exemplo.com.br')).toBeInTheDocument()
    expect(within(linha).getByText('(11) 98765-4321')).toBeInTheDocument()
  })

  it('mostra traço onde o dado não foi informado', async () => {
    await renderList()

    const linha = screen.getByRole('row', { name: /Dr\. Roberto Almeida/ })
    expect(within(linha).getAllByText('—')).toHaveLength(2) // sem documento e sem telefone
  })

  it('filtra por tipo no controle segmentado e volta a "Todos"', async () => {
    const { user } = await renderList()
    const chip = screen.getByRole('radio', { name: 'Testemunha' })
    expect(screen.getByRole('radio', { name: 'Todos' })).toBeChecked()

    await user.click(chip)
    expect(chip).toBeChecked()
    await waitFor(() =>
      expect(screen.queryByText('Carlos Eduardo Pereira')).not.toBeInTheDocument()
    )
    expect(screen.getByText('Helena Rocha Martins')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'Todos' }))
    expect(await screen.findByText('Carlos Eduardo Pereira')).toBeInTheDocument()
  })

  it('busca por e-mail', async () => {
    const { user } = await renderList()

    await user.type(screen.getByRole('searchbox'), 'horizonte.com')

    await waitFor(() =>
      expect(screen.queryByText('Carlos Eduardo Pereira')).not.toBeInTheDocument()
    )
    expect(screen.getByText('Construtora Horizonte S.A.')).toBeInTheDocument()
  })

  it('cada linha tem ações nomeadas com o contato', async () => {
    await renderList()

    expect(screen.getByRole('link', { name: 'Editar Carlos Eduardo Pereira' })).toHaveAttribute(
      'href',
      '/contatos/contato-001/editar'
    )
    expect(screen.getByRole('button', { name: 'Excluir Carlos Eduardo Pereira' })).toBeVisible()
  })

  it('exclui só depois de confirmar', async () => {
    const { user } = await renderList()

    await user.click(screen.getByRole('button', { name: 'Excluir Carlos Eduardo Pereira' }))
    const dialogo = screen.getByRole('alertdialog')
    expect(dialogo).toHaveTextContent('Carlos Eduardo Pereira')
    // Cancelar não exclui.
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    expect(screen.getByText('Carlos Eduardo Pereira')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Excluir Carlos Eduardo Pereira' }))
    await user.click(
      within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Excluir' })
    )

    await waitFor(() =>
      expect(screen.queryByText('Carlos Eduardo Pereira')).not.toBeInTheDocument()
    )
  })
})
