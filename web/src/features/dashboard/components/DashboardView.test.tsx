import { screen, waitFor, within } from '@testing-library/react'
import { Suspense } from 'react'
import * as agendaApi from '@/features/agenda/api'
import type { AgendaEvento } from '@/features/agenda/types'
import * as processosApi from '@/features/processos/api'
import * as publicationsApi from '@/features/publicacoes/api'
import type { Publication } from '@/features/publicacoes/types'
import { useNow } from '@/hooks/useNow'
import { mockUser } from '@/lib/auth/mockAuth'
import type { AccountType, UserRole } from '@/lib/auth/types'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useAuthStore } from '@/stores/authStore'
import { useUiStore } from '@/stores/uiStore'
import { createTestQueryClient, renderWithProviders } from '@/test/render'
import type { DashboardSummary } from '../types'
import { DashboardView } from './DashboardView'

vi.mock('@/hooks/useNow', () => ({ useNow: vi.fn() }))
vi.mock('@/features/agenda/api')
vi.mock('@/features/publicacoes/api')
vi.mock('@/features/processos/api')
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const NOW = new Date(2026, 10, 5, 14, 0) // quinta, 5/nov/2026, 14:00

const resumo: DashboardSummary = {
  metrics: {
    activeProcesses: { value: 12, delta: 0 },
    unreadPublications: { value: 7, delta: 0 },
    pendingReviews: { value: 3, delta: 0 },
    unreadAlerts: { value: 2, delta: 0 },
  },
  publications: [],
  alerts: [],
}

const evento = (
  id: string,
  dias: number,
  hora: number,
  extra: Partial<AgendaEvento> = {}
): AgendaEvento => ({
  id,
  titulo: `Evento ${id}`,
  descricao: null,
  tipo: 'Prazo',
  inicio: new Date(2026, 10, 5 + dias, hora).toISOString(),
  fim: new Date(2026, 10, 5 + dias, hora + 1).toISOString(),
  diaInteiro: false,
  local: null,
  processoId: null,
  responsavelId: null,
  ...extra,
})

const eventos: AgendaEvento[] = [
  evento('p16', 0, 16, { titulo: 'Contestação Souza', processoId: 'proc-1' }),
  evento('p18', 0, 18, { titulo: 'Réplica Reis' }),
  evento('aud', 0, 15, { tipo: 'Audiencia', titulo: 'Audiência de instrução' }),
  evento('amanha', 1, 9, { titulo: 'Embargos de declaração' }),
  evento('longe', 3, 9, { tipo: 'Reuniao', titulo: 'Reunião de equipe' }),
]

const revisao = {
  id: 'pub-1',
  processNumber: '0001234-55.2025.8.06.0001',
  tribunal: 'TJCE',
  journal: 'DJEN',
} as unknown as Publication

function entrar(role: UserRole, accountType: AccountType | null = 'Firm') {
  useAuthStore.setState({ user: { ...mockUser, name: 'Dra. Ana Souza', role, accountType } })
}

function render() {
  const queryClient = createTestQueryClient()
  queryClient.setQueryData(queryKeys.dashboard.summary(), resumo)

  return renderWithProviders(
    <Suspense fallback={null}>
      <DashboardView />
    </Suspense>,
    { queryClient }
  )
}

beforeEach(() => {
  vi.mocked(useNow).mockReturnValue(NOW)
  vi.mocked(agendaApi.listEventos).mockResolvedValue(eventos)
  vi.mocked(publicationsApi.getPublications).mockResolvedValue({
    items: [revisao],
    page: 1,
    pageSize: 5,
    totalCount: 1,
    totalPages: 1,
  })
  vi.mocked(processosApi.listProcessos).mockResolvedValue({
    items: [
      {
        id: 'proc-1',
        cnjNumber: '0000777-07.2018.8.06.0100',
        clienteId: 'c1',
        area: 'Civel',
        status: 'Ativo',
        tribunal: 'TJCE',
        parteContraria: null,
        dataDistribuicao: null,
      },
    ],
    page: 1,
    pageSize: 100,
    totalCount: 1,
    totalPages: 1,
  })
})

afterEach(() => {
  vi.resetAllMocks()
  useAuthStore.setState({ user: null })
  useUiStore.setState({ dashboardVariant: 'A' })
})

describe('DashboardView — cabeçalho', () => {
  it('saúda pelo primeiro nome, sem o tratamento, e mostra a data por extenso', () => {
    entrar('Lawyer')
    render()

    expect(screen.getByRole('heading', { level: 1, name: 'Boa tarde, Ana.' })).toBeInTheDocument()
    expect(screen.getByText('Quinta-feira, 5 de novembro')).toBeInTheDocument()
  })

  it('antes de hidratar (sem "agora") não chuta hora nem data', () => {
    vi.mocked(useNow).mockReturnValue(null)
    entrar('Lawyer')
    render()

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Olá, Ana.')
    expect(screen.queryByText(/de novembro/)).toBeNull()
    expect(agendaApi.listEventos).not.toHaveBeenCalled()
  })
})

describe('DashboardView — advogado(a), variação A', () => {
  it('mostra os quatro indicadores com os números reais e sem inventar tarefas', async () => {
    entrar('Lawyer')
    render()

    const indicadores = screen.getByRole('region', { name: 'Indicadores' })
    expect(within(indicadores).getByText('Tarefas atrasadas')).toBeInTheDocument()
    // Sem módulo de tarefas não há número: "—" e a explicação, nunca um zero que pareça verdade.
    expect(within(indicadores).getByText(/disponível com o módulo de tarefas/)).toBeInTheDocument()
    await waitFor(() =>
      expect(within(indicadores).getByText('próximo às 16:00')).toBeInTheDocument()
    )

    const prazos = within(indicadores).getByRole('link', { name: /Prazos hoje/ })
    expect(within(prazos).getByText('2')).toBeInTheDocument()
    expect(within(indicadores).getByRole('link', { name: /Para revisar/ })).toHaveAttribute(
      'href',
      '/publicacoes?status=untreated'
    )
    expect(within(indicadores).getByRole('link', { name: /Alertas/ })).toHaveAttribute(
      'href',
      '/alertas'
    )
  })

  it('busca só a agenda do usuário logado (a fila é do advogado, não do escritório)', async () => {
    entrar('Lawyer')
    render()

    await waitFor(() => expect(agendaApi.listEventos).toHaveBeenCalled())
    expect(vi.mocked(agendaApi.listEventos).mock.calls[0]![1]).toBe(mockUser.id)
  })

  it('a fila ordena por urgência, resolve o número do processo e marca a revisão da IA', async () => {
    entrar('Lawyer')
    render()

    const fila = await screen.findByRole('region', { name: 'Fila de trabalho' })
    await within(fila).findByText('0000777-07.2018.8.06.0100')
    const titulos = within(fila)
      .getAllByRole('link')
      .map((link) => link.textContent ?? '')

    // Hoje, por horário: audiência 15h, contestação 16h, réplica 18h; depois amanhã; revisões por último.
    expect(titulos[0]).toContain('Audiência de instrução')
    expect(titulos[1]).toContain('Contestação Souza')
    expect(titulos[2]).toContain('Réplica Reis')
    expect(titulos[titulos.length - 1]).toContain('Para revisar')
    expect(titulos.findIndex((t) => t.includes('Embargos'))).toBeGreaterThan(
      titulos.findIndex((t) => t.includes('Réplica Reis'))
    )
    expect(within(fila).getByText('(sugerido pela IA)')).toBeInTheDocument()
  })

  it('só prazo "vence hoje"; audiência e reunião de hoje aparecem como "Hoje"', async () => {
    entrar('Lawyer')
    render()
    const fila = await screen.findByRole('region', { name: 'Fila de trabalho' })
    await within(fila).findByText('Audiência de instrução')

    const linha = (titulo: string) => within(fila).getByText(titulo).closest('a')!
    expect(within(linha('Réplica Reis')).getByText('Vence hoje')).toBeInTheDocument()
    expect(within(linha('Audiência de instrução')).getByText('Hoje')).toBeInTheDocument()
    expect(within(linha('Audiência de instrução')).queryByText('Vence hoje')).toBeNull()
  })

  it('"Hoje e atrasados" e "Sugeridos pela IA" filtram a fila', async () => {
    entrar('Lawyer')
    const { user } = render()
    const fila = await screen.findByRole('region', { name: 'Fila de trabalho' })
    await within(fila).findByText('Réplica Reis')

    await user.click(within(fila).getByRole('radio', { name: 'Hoje e atrasados' }))
    expect(within(fila).queryByText('Embargos de declaração')).toBeNull()
    expect(within(fila).getByText('Réplica Reis')).toBeInTheDocument()

    await user.click(within(fila).getByRole('radio', { name: 'Sugeridos pela IA' }))
    expect(within(fila).queryByText('Réplica Reis')).toBeNull()
    expect(within(fila).getByText(/Revisar sugestão de prazo/)).toBeInTheDocument()
  })

  it('se a agenda falhar, mostra o erro com "tentar de novo" em vez de uma fila vazia', async () => {
    vi.mocked(agendaApi.listEventos).mockRejectedValue(new Error('falha'))
    entrar('Lawyer')
    render()

    expect(await screen.findByText('Não foi possível carregar os prazos')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /tentar novamente/i })).toBeInTheDocument()
  })
})

describe('DashboardView — advogado(a), variação B', () => {
  it('troca para a leitura editorial, com a frase do dia e a agenda de hoje', async () => {
    entrar('Lawyer')
    const { user } = render()

    await user.click(screen.getByRole('radio', { name: 'Variação B' }))

    await waitFor(() =>
      expect(screen.getByText(/vencem/)).toHaveTextContent(
        'Hoje vencem 2 prazos, e 3 publicações aguardam sua revisão.'
      )
    )
    const agenda = screen.getByRole('region', { name: 'Agenda de hoje' })
    expect(within(agenda).getByText('Audiência de instrução')).toBeInTheDocument()
    expect(within(agenda).queryByText('Embargos de declaração')).toBeNull() // é de amanhã
    expect(useUiStore.getState().dashboardVariant).toBe('B')
  })

  it('concorda singular e plural na frase', async () => {
    vi.mocked(agendaApi.listEventos).mockResolvedValue([evento('so', 0, 18)])
    entrar('Lawyer')
    useUiStore.setState({ dashboardVariant: 'B' })
    render()

    await waitFor(() =>
      expect(screen.getByText(/Hoje vence/)).toHaveTextContent('Hoje vence 1 prazo,')
    )
  })
})

describe('DashboardView — perfis', () => {
  it('advogado autônomo (Owner em conta Solo) vê o painel de advogado, não o de sócio', async () => {
    entrar('Owner', 'Solo')
    render()

    expect(await screen.findByRole('region', { name: 'Fila de trabalho' })).toBeInTheDocument()
    expect(screen.queryByText('Aprovações pendentes')).toBeNull()
  })

  it('sócio vê aprovações, carga da equipe e riscos de prazo, com o selo de demonstração', async () => {
    entrar('Owner')
    render()

    expect(screen.getByText('Aprovações pendentes')).toBeInTheDocument()
    expect(screen.getByText('Carga da equipe')).toBeInTheDocument()
    expect(screen.getAllByText('Demonstração').length).toBeGreaterThanOrEqual(2)
    // Riscos de prazo são reais, então não levam o selo.
    expect(await screen.findByText('Contestação Souza')).toBeInTheDocument()
    expect(vi.mocked(agendaApi.listEventos).mock.calls[0]![1]).toBeNull() // agenda do escritório inteiro
  })

  it('sócio aprova: o item some e aparece o aviso; peça vai para revisão', async () => {
    entrar('Partner')
    const { user } = render()

    await user.click(screen.getByRole('button', { name: /Aprovar: Proposta de êxito/ }))
    expect(notify.success).toHaveBeenLastCalledWith('Aprovado')
    expect(screen.queryByText(/Proposta de êxito/)).toBeNull()

    await user.click(
      screen.getByRole('button', { name: /Revisar: Contestação · Souza × Banco Alfa/ })
    )
    expect(notify.success).toHaveBeenLastCalledWith('Peça enviada para sua revisão')
  })

  it('sócio decide todas as aprovações e vê o estado vazio', async () => {
    entrar('Owner')
    const { user } = render()

    for (const botao of screen.getAllByRole('button', { name: /^(Aprovar|Revisar):/ })) {
      await user.click(botao)
    }

    expect(screen.getByText('Nenhuma aprovação pendente.')).toBeInTheDocument()
  })

  it('secretária: marcar o checklist atualiza o indicador "A protocolar"', async () => {
    entrar('Admin')
    const { user } = render()
    const indicadores = screen.getByRole('region', { name: 'Indicadores' })
    const protocolar = () => within(indicadores).getByText('A protocolar').closest('div')!

    expect(within(protocolar()).getByText('3')).toBeInTheDocument()
    await user.click(screen.getByRole('checkbox', { name: /Protocolar contestação/ }))
    expect(within(protocolar()).getByText('2')).toBeInTheDocument()
  })

  it.each(['Admin', 'Intern', 'Financial'] as const)(
    '%s não dispara nenhuma consulta: a API lhe recusaria publicações, processos e o resumo',
    async (role) => {
      entrar(role)
      render()
      await new Promise((resolve) => setTimeout(resolve, 20))

      expect(publicationsApi.getPublications).not.toHaveBeenCalled()
      expect(processosApi.listProcessos).not.toHaveBeenCalled()
      expect(agendaApi.listEventos).not.toHaveBeenCalled()
    }
  )

  it('sócio só busca a agenda: não monta fila (sem publicações nem processos)', async () => {
    entrar('Owner')
    render()
    await screen.findByText('Contestação Souza')

    expect(publicationsApi.getPublications).not.toHaveBeenCalled()
    expect(processosApi.listProcessos).not.toHaveBeenCalled()
  })

  it('financeiro: gráfico com alternativa em texto e conciliação', async () => {
    entrar('Financial')
    const { user } = render()

    expect(screen.getByRole('table', { name: /Entradas e saídas por mês/ })).toBeInTheDocument()
    await user.click(
      screen.getByRole('button', { name: /Conciliar: Honorários · Reis Transportes/ })
    )
    expect(
      screen.getByRole('button', { name: /Conciliado: Honorários · Reis Transportes/ })
    ).toBeDisabled()
  })
})
