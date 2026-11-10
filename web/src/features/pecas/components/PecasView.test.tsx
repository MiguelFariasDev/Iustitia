import { screen, waitFor, within } from '@testing-library/react'
import * as documentosApi from '@/features/documentos/api'
import * as processosApi from '@/features/processos/api'
import { notify } from '@/lib/notify'
import { renderWithProviders } from '@/test/render'
import * as pecasApi from '../api'
import { montarMinuta } from '../modelos'
import { PecasView } from './PecasView'

const envMock = vi.hoisted(() => ({ useMocks: true }))
vi.mock('@/config/env', () => ({ env: envMock, isMockAuth: false }))
vi.mock('@/features/documentos/api')
vi.mock('@/features/processos/api')
vi.mock('../api')
vi.mock('@/lib/api/toastHandler', () => ({ handleApiError: vi.fn() }))
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const processo = {
  id: 'proc-1',
  cnjNumber: '0000777-07.2018.8.06.0100',
  clienteId: 'c1',
  area: 'Civel' as const,
  status: 'Ativo' as const,
  tribunal: 'TJCE',
  parteContraria: 'Banco Alfa S.A.',
  dataDistribuicao: null,
}

beforeEach(() => {
  envMock.useMocks = true
  vi.mocked(processosApi.listProcessos).mockResolvedValue({
    items: [processo],
    page: 1,
    pageSize: 100,
    totalCount: 1,
    totalPages: 1,
  })
  vi.mocked(pecasApi.gerarMinuta).mockImplementation(async (tipo, dados) =>
    montarMinuta(tipo, dados)
  )
  vi.mocked(documentosApi.uploadDocumento).mockResolvedValue({} as never)
})

afterEach(() => vi.resetAllMocks())

async function escolherProcesso(user: ReturnType<typeof renderWithProviders>['user']) {
  await user.click(screen.getByRole('combobox', { name: 'Processo' }))
  await user.click(await screen.findByRole('option', { name: /0000777-07\.2018\.8\.06\.0100/ }))
}

describe('PecasView', () => {
  it('avisa que são modelos de demonstração, não peças prontas', () => {
    renderWithProviders(<PecasView />)

    expect(screen.getByRole('note')).toHaveTextContent(/Modelos de demonstração/)
    expect(screen.getByRole('note')).toHaveTextContent(/lacunas/)
  })

  it('"Gerar minuta com IA" só habilita depois de escolher o processo', async () => {
    const { user } = renderWithProviders(<PecasView />)
    const gerar = screen.getByRole('button', { name: /Gerar minuta com IA/ })
    expect(gerar).toBeDisabled()

    await escolherProcesso(user)

    expect(gerar).toBeEnabled()
  })

  it('gera a minuta do tipo e do processo escolhidos, como rascunho que exige revisão', async () => {
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)

    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    const minuta = await screen.findByRole('region', { name: 'Minuta gerada' })
    expect(pecasApi.gerarMinuta).toHaveBeenCalledWith(
      'contestacao',
      expect.objectContaining({
        cnjNumber: '0000777-07.2018.8.06.0100',
        parteContraria: 'Banco Alfa S.A.',
      })
    )
    expect(within(minuta).getByText(/exige revisão do advogado/)).toBeInTheDocument()
    expect(within(minuta).getByRole('heading', { name: 'CONTESTAÇÃO' })).toBeInTheDocument()
    expect(
      within(minuta).getByText(/Processo nº 0000777-07\.2018\.8\.06\.0100/)
    ).toBeInTheDocument()
  })

  it('destaca as citações legais com <mark>', async () => {
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))
    await screen.findByRole('region', { name: 'Minuta gerada' })

    const marcas = Array.from(document.querySelectorAll('mark')).map((m) => m.textContent)

    expect(marcas).toContain('art. 335 do CPC')
  })

  it('durante a geração mostra o carregamento e trava o botão', async () => {
    let concluir: (valor: ReturnType<typeof montarMinuta>) => void = () => undefined
    vi.mocked(pecasApi.gerarMinuta).mockReturnValue(new Promise((resolve) => (concluir = resolve)))
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)

    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    expect(screen.getByText('Gerando a minuta…')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()

    concluir(montarMinuta('contestacao', { ...processo, comarca: null, vara: null }))
    expect(await screen.findByRole('region', { name: 'Minuta gerada' })).toBeInTheDocument()
  })

  it('trocar o tipo descarta a minuta: ela era de outra peça', async () => {
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))
    await screen.findByRole('region', { name: 'Minuta gerada' })

    await user.click(screen.getByRole('combobox', { name: 'Tipo de peça' }))
    await user.click(await screen.findByRole('option', { name: 'Recurso de apelação' }))

    expect(screen.queryByRole('region', { name: 'Minuta gerada' })).toBeNull()
  })

  it('salvar cria o rascunho em Documentos, vinculado ao processo, com o texto da minuta', async () => {
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))
    await screen.findByRole('region', { name: 'Minuta gerada' })

    await user.click(screen.getByRole('button', { name: 'Salvar como rascunho' }))

    await waitFor(() => expect(documentosApi.uploadDocumento).toHaveBeenCalled())
    const valores = vi.mocked(documentosApi.uploadDocumento).mock.calls[0]![0]
    expect(valores.file.name).toBe('Rascunho - CONTESTAÇÃO - 0000777-07.2018.8.06.0100.txt')
    expect(valores).toMatchObject({
      vinculoTipo: 'Processo',
      vinculoRotulo: '0000777-07.2018.8.06.0100',
    })
    expect(await valores.file.text()).toContain('CONTESTAÇÃO')
    expect(notify.success).toHaveBeenCalledWith('Minuta salva em Documentos como rascunho')
    expect(await screen.findByRole('button', { name: 'Salva em Documentos' })).toBeDisabled()
  })

  it('se salvar falhar, o botão continua disponível para tentar de novo', async () => {
    vi.mocked(documentosApi.uploadDocumento).mockRejectedValue(new Error('falha'))
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))
    await screen.findByRole('region', { name: 'Minuta gerada' })

    await user.click(screen.getByRole('button', { name: 'Salvar como rascunho' }))

    await waitFor(() => expect(documentosApi.uploadDocumento).toHaveBeenCalled())
    expect(notify.success).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Salvar como rascunho' })).toBeEnabled()
  })

  it('a exportação para PDF/DOCX aparece como indisponível, sem fingir que funciona', async () => {
    const { user } = renderWithProviders(<PecasView />)
    await escolherProcesso(user)
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))
    await screen.findByRole('region', { name: 'Minuta gerada' })

    expect(screen.getByRole('button', { name: /Exportar PDF\/DOCX/ })).toBeDisabled()
  })

  it('sem processos reais, em demonstração, oferece um processo de exemplo', async () => {
    vi.mocked(processosApi.listProcessos).mockRejectedValue(new Error('sem API'))
    const { user } = renderWithProviders(<PecasView />)

    await user.click(screen.getByRole('combobox', { name: 'Processo' }))

    expect(
      await screen.findByRole('option', { name: /0000777-07\.2018\.8\.06\.0100/ })
    ).toBeInTheDocument()
  })

  it('sem mocks não há geração: a tela diz isso, sem controles nem consulta', () => {
    envMock.useMocks = false
    renderWithProviders(<PecasView />)

    expect(
      screen.getByText('A criação de peças com IA ainda não está disponível')
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Gerar minuta/ })).toBeNull()
    expect(processosApi.listProcessos).not.toHaveBeenCalled()
  })
})
