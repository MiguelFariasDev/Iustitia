import { screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { mockGetPublication, resetPublicationsMock } from '../mocks'
import { PublicacaoDetail } from './PublicacaoDetail'

const ID = 'pub-002' // não lida no seed (i=1)

beforeEach(() => resetPublicationsMock())

describe('PublicacaoDetail', () => {
  it('mostra os dados, o texto integral e o vínculo com processo e cliente', async () => {
    renderWithProviders(<PublicacaoDetail publicationId={ID} />)

    expect(await screen.findByRole('heading', { name: 'Dados da publicação' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /^5001001/ })).toHaveAttribute(
      'href',
      '/processos/proc-2'
    )
    expect(screen.getByRole('link', { name: 'Transportadora Horizonte Ltda.' })).toHaveAttribute(
      'href',
      '/clientes/cli-2'
    )
    expect(screen.getByRole('region', { name: 'Texto da publicação' })).toHaveTextContent(
      /Vistos\. Designo audiência/
    )
  })

  it('abrir a publicação a marca como lida — mas NÃO muda o status de revisão', async () => {
    renderWithProviders(<PublicacaoDetail publicationId={ID} />)
    await screen.findByRole('heading', { name: 'Dados da publicação' })

    await waitFor(() => expect(mockGetPublication(ID)?.isRead).toBe(true))
    expect(mockGetPublication(ID)?.status).toBe('untreated')
  })

  it('marcar como tratada é uma ação explícita e fica indisponível depois', async () => {
    const { user } = renderWithProviders(<PublicacaoDetail publicationId={ID} />)
    await screen.findByRole('heading', { name: 'Dados da publicação' })

    await user.click(screen.getByRole('button', { name: 'Marcar como tratada' }))

    await waitFor(() => expect(mockGetPublication(ID)?.status).toBe('treated'))
    await waitFor(() =>
      expect(screen.getByRole('button', { name: 'Marcar como tratada' })).toBeDisabled()
    )
    // Arquivar volta a ficar disponível quando a ação termina (enquanto roda, tudo fica travado).
    await waitFor(() => expect(screen.getByRole('button', { name: 'Arquivar' })).toBeEnabled())
  })

  it('arquivar desabilita arquivar de novo', async () => {
    const { user } = renderWithProviders(<PublicacaoDetail publicationId={ID} />)
    await screen.findByRole('heading', { name: 'Dados da publicação' })

    await user.click(screen.getByRole('button', { name: 'Arquivar' }))

    await waitFor(() => expect(screen.getByRole('button', { name: 'Arquivar' })).toBeDisabled())
  })

  it('o link para o DJEN abre em nova aba, com rel seguro', async () => {
    renderWithProviders(<PublicacaoDetail publicationId={ID} />)
    await screen.findByRole('heading', { name: 'Dados da publicação' })

    const link = screen.getByRole('link', { name: /Acessar no DJEN/ })
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    expect(link).toHaveAttribute('href', 'https://comunicaapi.pje.jus.br/')
  })

  it('tem abas de texto, metadados e histórico', async () => {
    const { user } = renderWithProviders(<PublicacaoDetail publicationId={ID} />)
    await screen.findByRole('heading', { name: 'Dados da publicação' })

    await user.click(screen.getByRole('tab', { name: 'Metadados' }))
    // O mesmo valor aparece no cartão de dados (Classe) e na aba de metadados.
    await waitFor(() => expect(screen.getAllByText('Procedimento Comum Cível')).toHaveLength(2))
    expect(screen.getByText('classe')).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Histórico' }))
    const history = await screen.findByText('Divulgada no diário')
    expect(within(history.closest('ol')!).getByText('Situação atual')).toBeInTheDocument()
  })

  it('publicação inexistente mostra erro com tentar novamente', async () => {
    renderWithProviders(<PublicacaoDetail publicationId="nao-existe" />)

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Não foi possível carregar a publicação'
    )
    expect(screen.getByRole('button', { name: 'Tentar novamente' })).toBeInTheDocument()
  })
})
