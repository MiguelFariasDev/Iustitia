import { screen, waitFor, within } from '@testing-library/react'
import { useNow } from '@/hooks/useNow'
import { mockUser } from '@/lib/auth/mockAuth'
import type { UserRole } from '@/lib/auth/types'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useAuthStore } from '@/stores/authStore'
import { useTenantStore } from '@/stores/tenantStore'
import { navigationState, routerMock } from '@/test/navigation'
import { createTestQueryClient, renderWithProviders } from '@/test/render'
import * as api from '../api'
import type { Publication, PublicationsSummary } from '../types'
import { PublicacaoAtualizarButton } from './PublicacaoAtualizarButton'
import { PublicacaoResumoCards } from './PublicacaoResumoCards'
import { PublicacaoRowSummary } from './PublicacaoRow'
import { PublicacaoStatusBadge } from './PublicacaoStatusBadge'
import { PublicacoesView } from './PublicacoesView'

vi.mock('@/hooks/useNow', () => ({ useNow: vi.fn() }))
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))
vi.mock('../api')
vi.mock('@/features/clientes/queries', async () => {
  const { queryOptions } = await import('@tanstack/react-query')
  return {
    clientesQuery: () =>
      queryOptions({ queryKey: ['clientes-v4'], queryFn: () => Promise.resolve({ items: [] }) }),
  }
})

const NOW = new Date(2026, 10, 5, 14, 0) // quinta, 5/nov/2026

const resumo: PublicationsSummary = {
  total: 40,
  untreatedTotal: 27,
  unread: 9,
  untreatedToday: 4,
  treatedThisWeek: 6,
}

const pub = (extra: Partial<Publication> = {}): Publication =>
  ({
    id: 'pub-1',
    publishedAt: '2026-11-05T09:00:00Z',
    capturedAt: '2026-11-05T10:00:00Z',
    kind: 'intimation',
    processId: 'proc-1',
    processNumber: '0000777-07.2018.8.06.0100',
    tribunal: 'TJCE',
    journal: 'DJEN',
    clienteId: null,
    clienteNome: null,
    status: 'untreated',
    isRead: false,
    content: 'Fica a parte intimada para se manifestar.',
    reviewNotes: null,
    metadata: null,
    ...extra,
  }) as Publication

function entrar(role: UserRole | null) {
  useAuthStore.setState({ user: role ? { ...mockUser, role } : null })
}

function iaLigada(ligada: boolean) {
  useTenantStore.setState({ featureFlags: { 'ai.classification': ligada } })
}

beforeEach(() => {
  vi.mocked(useNow).mockReturnValue(NOW)
  vi.mocked(api.getPublicationsSummary).mockResolvedValue(resumo)
  navigationState.searchParams = new URLSearchParams()
})

afterEach(() => {
  vi.resetAllMocks()
  useAuthStore.setState({ user: null })
  useTenantStore.getState().clear()
})

describe('visões rápidas', () => {
  beforeEach(() => {
    vi.mocked(api.getPublications).mockResolvedValue({
      items: [pub()],
      page: 1,
      pageSize: 20,
      totalCount: 1,
      totalPages: 1,
    })
  })

  it.each([
    ['Aguardando revisão', 'status=untreated'],
    ['Tratadas', 'status=treated'],
    ['Não lidas', 'isRead=false'],
  ])('"%s" aplica %s na URL, sem empilhar histórico', async (nome, esperado) => {
    const { user } = renderWithProviders(<PublicacoesView />)
    await screen.findByRole('radiogroup', { name: 'Visão da lista de publicações' })

    await user.click(screen.getByRole('radio', { name: nome }))

    expect(routerMock.replace).toHaveBeenCalledTimes(1)
    expect(String(routerMock.replace.mock.calls[0]![0])).toContain(esperado)
  })

  it('a visão marcada vem da URL', async () => {
    navigationState.searchParams = new URLSearchParams('status=untreated')
    renderWithProviders(<PublicacoesView />)

    expect(await screen.findByRole('radio', { name: 'Aguardando revisão' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Todas' })).not.toBeChecked()
  })

  it('"Todas" tira o status e a leitura da URL', async () => {
    navigationState.searchParams = new URLSearchParams('status=treated&tribunal=TJCE')
    const { user } = renderWithProviders(<PublicacoesView />)

    await user.click(await screen.findByRole('radio', { name: 'Todas' }))

    const alvo = String(routerMock.replace.mock.calls[0]![0])
    expect(alvo).not.toContain('status=')
    expect(alvo).toContain('tribunal=TJCE') // os outros filtros continuam
  })

  it('situação que nenhuma visão representa (arquivadas) não marca nenhuma opção', async () => {
    navigationState.searchParams = new URLSearchParams('status=archived')
    renderWithProviders(<PublicacoesView />)
    const grupo = await screen.findByRole('radiogroup', { name: 'Visão da lista de publicações' })

    expect(within(grupo).queryAllByRole('radio', { checked: true })).toHaveLength(0)
  })

  it('não repete como chip o que já é visão rápida (só descartadas, arquivadas e lidas)', async () => {
    renderWithProviders(<PublicacoesView />)
    await screen.findByRole('radiogroup', { name: 'Visão da lista de publicações' })

    expect(screen.queryByRole('button', { name: 'Tratada' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Não lidas' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Arquivada' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lidas' })).toBeInTheDocument()
  })
})

describe('cartões de resumo', () => {
  it('cada cartão leva à lista com o MESMO número que mostra (hoje e semana, no fuso local)', async () => {
    renderWithProviders(<PublicacaoResumoCards />)

    const hoje = await screen.findByRole('link', { name: /Não tratadas hoje/ })
    expect(hoje).toHaveAttribute(
      'href',
      '/publicacoes?status=untreated&from=2026-11-05&to=2026-11-05'
    )
    expect(within(hoje).getByText('4')).toBeInTheDocument()

    expect(screen.getByRole('link', { name: /Aguardando revisão/ })).toHaveAttribute(
      'href',
      '/publicacoes?status=untreated'
    )
    // Semana começa na segunda (2/nov).
    expect(screen.getByRole('link', { name: /Tratadas na semana/ })).toHaveAttribute(
      'href',
      '/publicacoes?status=treated&from=2026-11-02'
    )
  })

  it('antes de hidratar (sem "agora") os links saem sem data em vez de chutar uma', async () => {
    vi.mocked(useNow).mockReturnValue(null)
    renderWithProviders(<PublicacaoResumoCards />)

    expect(await screen.findByRole('link', { name: /Não tratadas hoje/ })).toHaveAttribute(
      'href',
      '/publicacoes?status=untreated'
    )
  })
})

describe('linha da lista', () => {
  it('o bloco todo é o link para a publicação, inclusive sem processo', () => {
    entrar('Lawyer')
    const { rerender } = renderWithProviders(<PublicacaoRowSummary item={pub()} />)
    expect(screen.getByRole('link')).toHaveAttribute('href', '/publicacoes/pub-1')

    rerender(<PublicacaoRowSummary item={pub({ processNumber: null, processId: null })} />)
    expect(screen.getByRole('link')).toHaveAttribute('href', '/publicacoes/pub-1')
    expect(screen.getByText('Processo não encontrado')).toBeInTheDocument()
  })

  it('não lida: bolinha violeta e texto em destaque; lida: sem bolinha', () => {
    entrar('Lawyer')
    const { rerender } = renderWithProviders(<PublicacaoRowSummary item={pub({ isRead: false })} />)
    expect(screen.getByRole('img', { name: 'Não lida' })).toBeInTheDocument()
    expect(screen.getByText(/Fica a parte intimada/)).toHaveClass('font-semibold')

    rerender(<PublicacaoRowSummary item={pub({ isRead: true })} />)
    expect(screen.queryByRole('img', { name: 'Não lida' })).toBeNull()
  })

  it('quem não é advogado nem sócio não abre revisão da IA: recebe o aviso e não navega', async () => {
    entrar('Admin')
    iaLigada(true)
    const { user } = renderWithProviders(<PublicacaoRowSummary item={pub()} />)
    // O React trata o clique na raiz; o document vem depois e já enxerga o cancelamento.
    let cancelado = false
    const ouvir = (e: Event) => (cancelado = e.defaultPrevented)
    document.addEventListener('click', ouvir)

    await user.click(screen.getByRole('link'))
    document.removeEventListener('click', ouvir)

    expect(notify.info).toHaveBeenCalledWith('Revisão da IA é feita por advogado ou sócio')
    expect(cancelado).toBe(true) // navegação cancelada
  })

  it.each(['Owner', 'Partner', 'Lawyer'] as const)(
    '%s abre a revisão normalmente',
    async (role) => {
      entrar(role)
      iaLigada(true)
      const { user } = renderWithProviders(<PublicacaoRowSummary item={pub()} />)

      await user.click(screen.getByRole('link'))

      expect(notify.info).not.toHaveBeenCalled()
    }
  )

  it('sem a IA ligada o aviso não existe: não há "revisão da IA" para negar', async () => {
    entrar('Admin')
    iaLigada(false)
    const { user } = renderWithProviders(<PublicacaoRowSummary item={pub()} />)

    await user.click(screen.getByRole('link'))

    expect(notify.info).not.toHaveBeenCalled()
  })
})

describe('selo de status', () => {
  it('com a IA ligada: "Revisão da IA", que não afirma ter sugerido prazo', () => {
    iaLigada(true)
    renderWithProviders(<PublicacaoStatusBadge publication={pub()} />)

    expect(screen.getByText('Revisão da IA')).toBeInTheDocument()
    expect(screen.queryByText(/sugeriu/i)).toBeNull()
  })

  it('sem a IA: "Aguardando revisão"; sem processo: "Sem processo"; tratada: "Tratada"', () => {
    iaLigada(false)
    const { rerender } = renderWithProviders(<PublicacaoStatusBadge publication={pub()} />)
    expect(screen.getByText('Aguardando revisão')).toBeInTheDocument()

    rerender(<PublicacaoStatusBadge publication={pub({ processId: null })} />)
    expect(screen.getByText('Sem processo')).toBeInTheDocument()

    rerender(<PublicacaoStatusBadge publication={pub({ status: 'treated' })} />)
    expect(screen.getByText('Tratada')).toBeInTheDocument()
  })
})

describe('botão Atualizar', () => {
  function renderBotao(antes: PublicationsSummary, depois: PublicationsSummary) {
    const queryClient = createTestQueryClient()
    queryClient.setQueryData(queryKeys.publications.summary(), antes, {
      updatedAt: new Date(2026, 10, 5, 6, 0).getTime(),
    })
    // 1ª chamada devolve o resumo já em cache (montagem); as seguintes, o "depois".
    vi.mocked(api.getPublicationsSummary).mockResolvedValueOnce(antes).mockResolvedValue(depois)
    // Na tela real os cartões observam o resumo; só consulta ATIVA é refeita pela invalidação.
    return renderWithProviders(
      <>
        <PublicacaoResumoCards />
        <PublicacaoAtualizarButton />
      </>,
      { queryClient }
    )
  }

  it('sem publicação nova, diz desde quando: a hora em que a contagem anterior foi carregada', async () => {
    const { user } = renderBotao(resumo, resumo)

    // Na montagem os cartões refazem o resumo (o cache nasce velho): espera o botão voltar.
    await user.click(await screen.findByRole('button', { name: 'Atualizar' }))

    await waitFor(() => expect(notify.info).toHaveBeenCalled())
    expect(vi.mocked(notify.info).mock.calls[0]![0]).toMatch(
      /^Nenhuma publicação nova desde \d{2}:\d{2}$/
    )
  })

  it('com publicações novas, conta quantas (singular e plural)', async () => {
    const { user } = renderBotao(resumo, { ...resumo, total: 43 })

    // Na montagem os cartões refazem o resumo (o cache nasce velho): espera o botão voltar.
    await user.click(await screen.findByRole('button', { name: 'Atualizar' }))

    await waitFor(() => expect(notify.success).toHaveBeenCalledWith('3 publicações novas'))
  })
})
