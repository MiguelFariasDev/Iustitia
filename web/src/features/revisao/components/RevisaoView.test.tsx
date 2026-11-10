import { screen, waitFor, within } from '@testing-library/react'
import * as agendaApi from '@/features/agenda/api'
import * as publicationsApi from '@/features/publicacoes/api'
import type { Publication } from '@/features/publicacoes/types'
import { notify } from '@/lib/notify'
import { useUiStore } from '@/stores/uiStore'
import { renderWithProviders } from '@/test/render'
import { useRevisaoStore } from '../store'
import { RevisaoView } from './RevisaoView'

const envMock = vi.hoisted(() => ({ useMocks: true }))
vi.mock('@/config/env', () => ({ env: envMock, isMockAuth: false }))
vi.mock('@/features/agenda/api')
vi.mock('@/features/publicacoes/api')
vi.mock('@/lib/api/toastHandler', () => ({ handleApiError: vi.fn() }))
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const pub = (id: string, extra: Partial<Publication>): Publication =>
  ({
    id,
    publishedAt: '2026-11-05T12:00:00Z',
    capturedAt: '2026-11-05T13:00:00Z',
    kind: 'citation',
    processId: `proc-${id}`,
    processNumber: `0000${id}-07.2018.8.06.0100`,
    tribunal: 'TJCE',
    journal: 'DJEN',
    clienteId: null,
    clienteNome: 'Maria Silva',
    status: 'untreated',
    isRead: false,
    content:
      'Intime-se a ré para contestar no prazo de 15 (quinze) dias úteis, sob pena de revelia.',
    reviewNotes: null,
    metadata: null,
    ...extra,
  }) as Publication

const publicacoes = [
  pub('1', {}),
  pub('2', {
    kind: 'intimation',
    publishedAt: '2026-11-04T12:00:00Z',
    content: 'Manifeste-se a autora no prazo de 5 (cinco) dias sobre a certidão.',
  }),
  pub('3', {
    kind: 'sentence',
    publishedAt: '2026-11-03T12:00:00Z',
    content: 'Sentença: julgo procedente o pedido.',
  }),
]

beforeEach(() => {
  envMock.useMocks = true
  vi.mocked(publicationsApi.getPublications).mockResolvedValue({
    items: publicacoes,
    page: 1,
    pageSize: 100,
    totalCount: 3,
    totalPages: 1,
  })
  vi.mocked(publicationsApi.changePublicationStatus).mockResolvedValue(undefined)
  vi.mocked(agendaApi.createEvento).mockResolvedValue({} as never)
})

afterEach(() => {
  vi.resetAllMocks()
  useRevisaoStore.getState().limpar()
  useUiStore.setState({ reviewVariant: 'A' })
})

async function renderRevisao() {
  const utils = renderWithProviders(<RevisaoView />)
  await screen.findByRole('tablist', { name: 'Publicações a revisar' })
  return utils
}

describe('RevisaoView — leitura (variação A)', () => {
  it('lista uma aba por publicação, numerada, com a providência sugerida', async () => {
    await renderRevisao()

    const abas = within(screen.getByRole('tablist')).getAllByRole('tab')
    expect(abas.map((a) => a.textContent)).toEqual([
      '01 Contestação',
      '02 Manifestação',
      '03 Recurso de apelação',
    ])
    expect(abas[0]).toHaveAttribute('aria-selected', 'true')
  })

  it('sinaliza que são sugestões de demonstração e que feriados não entram', async () => {
    await renderRevisao()

    expect(screen.getByRole('note')).toHaveTextContent(/Sugestões de demonstração/)
    expect(screen.getByRole('note')).toHaveTextContent(/ignora feriados/)
    expect(screen.getByText(/não considera feriados nem suspensões/)).toBeInTheDocument()
  })

  it('destaca o trecho que gerou o prazo e mostra tipo, prazo, vencimento e confiança', async () => {
    await renderRevisao()

    expect(document.querySelector('mark')).toHaveTextContent('15 (quinze) dias úteis')
    const ia = screen.getByRole('region', { name: 'Sugestão da IA' })
    expect(within(ia).getByText('Contestação')).toBeInTheDocument()
    expect(within(ia).getByText('15 dias úteis')).toBeInTheDocument()
    expect(within(ia).getByText('26/11/2026')).toBeInTheDocument() // 5/nov + 15 dias úteis
    expect(within(ia).getByText(/confiança \d+%/)).toBeInTheDocument()
  })

  it('a dica pode ser dispensada', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Dispensar dica' }))

    expect(screen.queryByText(/Revise antes de salvar/)).toBeNull()
  })

  it('lembra, no rodapé, que a responsabilidade é do advogado', async () => {
    await renderRevisao()

    expect(screen.getByText(/Responsabilidade final do advogado/)).toBeInTheDocument()
  })
})

describe('RevisaoView — decidir', () => {
  it('aceitar: salva o prazo na agenda, marca a publicação como aprovada e avança', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Aceitar' }))

    await waitFor(() => expect(publicationsApi.changePublicationStatus).toHaveBeenCalled())
    expect(agendaApi.createEvento).toHaveBeenCalledWith(
      expect.objectContaining({
        tipo: 'Prazo',
        diaInteiro: true,
        inicio: '2026-11-26T00:00',
        processoId: 'proc-1',
        titulo: 'Prazo: Contestação — 00001-07.2018.8.06.0100',
      })
    )
    expect(publicationsApi.changePublicationStatus).toHaveBeenCalledWith(
      '1',
      'Approved',
      expect.stringContaining('15 dias úteis')
    )
    expect(notify.success).toHaveBeenCalledWith('Prazo aceito e salvo na agenda')
    // Avanço automático para a próxima ainda não decidida.
    await waitFor(() =>
      expect(screen.getByRole('tab', { name: /02 Manifestação/ })).toHaveAttribute(
        'aria-selected',
        'true'
      )
    )
    expect(screen.getByRole('tab', { name: /01 Contestação/ })).toHaveTextContent('✓')
  })

  it('a descrição do evento leva só a referência, nunca o texto da publicação', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Aceitar' }))
    await waitFor(() => expect(agendaApi.createEvento).toHaveBeenCalled())

    const valores = vi.mocked(agendaApi.createEvento).mock.calls[0]![0]
    expect(valores.descricao).not.toContain('contestar')
    expect(valores.descricao).toMatch(/Estimativa sem feriados/)
  })

  it('rejeitar: só muda o status para rejeitada, sem criar prazo na agenda', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Rejeitar' }))

    await waitFor(() =>
      expect(publicationsApi.changePublicationStatus).toHaveBeenCalledWith(
        '1',
        'Rejected',
        expect.stringContaining('rejeitada')
      )
    )
    expect(agendaApi.createEvento).not.toHaveBeenCalled()
    expect(notify.success).toHaveBeenCalledWith('Sugestão rejeitada')
  })

  it('corrigir: o prazo vira campo numérico e confirmar salva o NOVO valor, registrando o original', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Corrigir' }))
    const campo = screen.getByLabelText('Prazo em dias úteis')
    expect(campo).toHaveValue(15)
    await user.clear(campo)
    await user.type(campo, '10')
    await user.click(screen.getByRole('button', { name: 'Confirmar' }))

    await waitFor(() =>
      expect(publicationsApi.changePublicationStatus).toHaveBeenCalledWith(
        '1',
        'Corrected',
        'Prazo corrigido de 15 para 10 dias úteis.'
      )
    )
    expect(agendaApi.createEvento).toHaveBeenCalledWith(
      expect.objectContaining({ inicio: '2026-11-19T00:00' }) // 5/nov + 10 dias úteis
    )
    expect(notify.success).toHaveBeenCalledWith('Prazo corrigido e salvo')
  })

  it.each(['-1', '1.5', '999', ''])('corrigir com "%s" não deixa confirmar', async (digitado) => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Corrigir' }))
    const campo = screen.getByLabelText('Prazo em dias úteis')
    await user.clear(campo)
    if (digitado) await user.type(campo, digitado)

    expect(screen.getByRole('button', { name: 'Confirmar' })).toBeDisabled()
    expect(publicationsApi.changePublicationStatus).not.toHaveBeenCalled()
  })

  it('enquanto corrige, "Aceitar" fica desabilitado: não dá para aceitar e corrigir ao mesmo tempo', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Corrigir' }))

    expect(screen.getByRole('button', { name: 'Aceitar' })).toBeDisabled()
  })

  it('se a API recusar, nada é marcado como decidido e os botões continuam disponíveis', async () => {
    vi.mocked(publicationsApi.changePublicationStatus).mockRejectedValue(new Error('409'))
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Aceitar' }))

    await waitFor(() => expect(publicationsApi.changePublicationStatus).toHaveBeenCalled())
    expect(notify.success).not.toHaveBeenCalled()
    expect(screen.queryByRole('status')).toBeNull()
    expect(screen.getByRole('button', { name: 'Aceitar' })).toBeEnabled()
  })

  it('decidida, a publicação mostra o resultado no lugar dos botões', async () => {
    const { user } = await renderRevisao()
    await user.click(screen.getByRole('button', { name: 'Aceitar' }))
    await waitFor(() => expect(notify.success).toHaveBeenCalled())

    await user.click(screen.getByRole('tab', { name: /01 Contestação/ }))

    expect(screen.getByRole('status')).toHaveTextContent('Prazo aceito e salvo na agenda')
    expect(screen.queryByRole('button', { name: 'Aceitar' })).toBeNull()
  })
})

describe('RevisaoView — variação B', () => {
  it('troca para a leitura com métricas e barra de decisão, e lembra a escolha', async () => {
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('radio', { name: 'Variação B' }))

    const resumo = screen.getByRole('region', { name: 'Resumo da sugestão' })
    expect(within(resumo).getByText('Tipo sugerido')).toBeInTheDocument()
    expect(within(resumo).getByText('Confiança')).toBeInTheDocument()
    expect(screen.getByText('Athena')).toBeInTheDocument() // nota de rodapé com o fundamento
    expect(screen.getByRole('button', { name: 'Corrigir prazo' })).toBeInTheDocument()
    expect(useUiStore.getState().reviewVariant).toBe('B')
  })

  it('decidir na variação B funciona igual', async () => {
    useUiStore.setState({ reviewVariant: 'B' })
    const { user } = await renderRevisao()

    await user.click(screen.getByRole('button', { name: 'Rejeitar' }))

    await waitFor(() => expect(notify.success).toHaveBeenCalledWith('Sugestão rejeitada'))
  })
})

describe('RevisaoView — estados', () => {
  it('sem publicações com processo, mostra o estado vazio', async () => {
    vi.mocked(publicationsApi.getPublications).mockResolvedValue({
      items: [pub('9', { processId: null })],
      page: 1,
      pageSize: 100,
      totalCount: 1,
      totalPages: 1,
    })
    renderWithProviders(<RevisaoView />)

    expect(await screen.findByText('Nenhuma publicação aguardando revisão')).toBeInTheDocument()
  })

  it('sem mocks não há sugestão da IA: a tela diz isso e não consulta nem inventa prazo', () => {
    envMock.useMocks = false
    renderWithProviders(<RevisaoView />)

    expect(screen.getByText('A revisão da IA ainda não está disponível')).toBeInTheDocument()
    expect(publicationsApi.getPublications).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Aceitar' })).toBeNull()
    expect(screen.queryByRole('radiogroup')).toBeNull()
  })
})
