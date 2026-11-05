import { screen, waitFor, within } from '@testing-library/react'
import { navigationState, routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { useAuthStore } from '@/stores/authStore'
import { resetMockAtendimentos } from '../mocks'
import { AtendimentosView } from './AtendimentosView'

beforeEach(() => {
  resetMockAtendimentos()
  navigationState.pathname = '/atendimentos'
  useAuthStore.getState().setSession({
    accessToken: 't',
    expiresAt: null,
    user: {
      id: 'adv-1',
      email: 'a@b.c',
      name: 'Ana',
      avatarUrl: null,
      role: 'Lawyer',
      tenantId: 'tenant-1',
    },
  })
})

afterEach(() => useAuthStore.getState().clear())

function renderView(query = '') {
  navigationState.searchParams = new URLSearchParams(query)
  return renderWithProviders(<AtendimentosView />)
}

const conversas = () => within(screen.getByRole('region', { name: 'Conversas' }))

describe('AtendimentosView — fila', () => {
  it('lista as conversas com o badge de cada status', async () => {
    renderView()

    const list = await screen.findByRole('list')
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(5)
    expect(within(items[0]!).getByText('Aguardando')).toBeInTheDocument()
    expect(within(list).getByText('Humano')).toBeInTheDocument()
    expect(within(list).getByText('Bot')).toBeInTheDocument()
    expect(within(list).getByText('Encerrado')).toBeInTheDocument()
  })

  it('sem conversa aberta, convida a selecionar uma', async () => {
    renderView()

    expect(await screen.findByText('Selecione uma conversa')).toBeInTheDocument()
  })

  it('as abas filtram por status', async () => {
    const { user } = renderView()
    await screen.findByRole('list')

    await user.click(screen.getByRole('tab', { name: 'Aguardando' }))

    await waitFor(() =>
      expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(2)
    )
    expect(within(screen.getByRole('list')).queryByText('Humano')).not.toBeInTheDocument()
  })

  it('busca por nome', async () => {
    const { user } = renderView()
    await screen.findByRole('list')

    await user.type(screen.getByLabelText('Buscar conversa'), 'joão')

    await waitFor(
      () => expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(1),
      {
        timeout: 2000,
      }
    )
    expect(screen.getByText('João Pereira')).toBeInTheDocument()
  })

  it('"meus atendimentos" esconde as conversas dos outros', async () => {
    const { user } = renderView()
    await screen.findByRole('list')

    await user.click(screen.getByLabelText('Somente os meus atendimentos'))

    expect(await screen.findByText('Nenhuma conversa corresponde aos filtros.')).toBeInTheDocument()
  })
})

describe('AtendimentosView — conversa aberta', () => {
  it('clicar numa conversa a coloca na URL (?c=) — dá para recarregar e compartilhar o link', async () => {
    const { user } = renderView()
    await screen.findByRole('list')

    await user.click(conversas().getByRole('button', { name: /Maria Souza/ }))

    expect(routerMock.replace).toHaveBeenCalledWith('/atendimentos?c=atd-001', { scroll: false })
  })

  it('uma conversa vinda da URL abre direto, com chat e contexto', async () => {
    renderView('c=atd-001')

    expect(await screen.findByRole('region', { name: 'Conversa' })).toBeInTheDocument()
    expect(await screen.findByLabelText('Contexto do atendimento')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: /Maria Souza/ })).toHaveAttribute(
      'aria-current',
      'true'
    )
  })

  it('o contexto mostra o contato e oferece vincular a um cliente quando não há cadastro', async () => {
    renderView('c=atd-001')

    const context = await screen.findByLabelText('Contexto do atendimento')
    expect(await within(context).findByText('Contato sem cadastro')).toBeInTheDocument()
    expect(within(context).getByText('+55 (85) 99999-8888')).toBeInTheDocument()
    expect(within(context).getByLabelText('Buscar cliente para vincular')).toBeInTheDocument()
  })

  it('"Voltar" fecha a conversa (layout estreito)', async () => {
    const { user } = renderView('c=atd-001')
    await screen.findByRole('region', { name: 'Conversa' })

    await user.click(await screen.findByRole('button', { name: 'Voltar para a lista' }))

    expect(routerMock.replace).toHaveBeenCalledWith('/atendimentos', { scroll: false })
  })
})
