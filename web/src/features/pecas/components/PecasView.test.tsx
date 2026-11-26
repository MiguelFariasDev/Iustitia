import { screen, waitFor } from '@testing-library/react'
import * as clientesApi from '@/features/clientes/api'
import * as processosApi from '@/features/processos/api'
import { renderWithProviders } from '@/test/render'
import type { UserEvent } from '@testing-library/user-event'
import * as pecasApi from '../api'
import type { AnexoCaso } from '../types'
import { PecasView } from './PecasView'

vi.mock('@/features/processos/api')
vi.mock('@/features/clientes/api')
vi.mock('../api')
vi.mock('@/lib/api/toastHandler', () => ({ handleApiError: vi.fn() }))
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const PECA_ID = '11111111-1111-1111-1111-111111111111'
const CASO = {
  id: 'caso-1',
  clienteId: 'cli-1',
  clienteNome: 'Empresa X',
  titulo: 'Cobrança',
  area: 'civel',
  processoId: null,
  status: 'Aberto',
  podeEscrever: true,
}

function anexo(extra: Partial<AnexoCaso> = {}): AnexoCaso {
  return {
    id: 'doc-1',
    nomeArquivo: 'inicial.pdf',
    status: 'Indexado',
    mensagem: null,
    origem: 'nativo',
    confiancaOcr: null,
    tipoDocumento: 'contrato',
    requerRevisaoHumana: false,
    revisado: false,
    conteudoSuspeito: false,
    ...extra,
  }
}

async function escolherCaso(user: UserEvent) {
  await user.click(await screen.findByRole('combobox', { name: 'Caso' }))
  await user.click(await screen.findByRole('option', { name: /Cobrança/ }))
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(pecasApi.listCasos).mockResolvedValue([CASO])
  vi.mocked(pecasApi.listAnexos).mockResolvedValue([])
  vi.mocked(clientesApi.listClientes).mockResolvedValue({
    items: [],
    page: 1,
    pageSize: 100,
    totalCount: 0,
    totalPages: 0,
  })
  vi.mocked(processosApi.listProcessos).mockResolvedValue({
    items: [],
    page: 1,
    pageSize: 100,
    totalCount: 0,
    totalPages: 0,
  })
  vi.mocked(pecasApi.submeterPeca).mockResolvedValue({
    id: PECA_ID,
    status: 'Pendente',
    mensagem: 'Na fila',
  })
  vi.mocked(pecasApi.getStatusPeca).mockResolvedValue({
    id: PECA_ID,
    status: 'Concluido',
    etapa: null,
    progresso: 100,
    mensagem: 'Pronta',
    erro: null,
  })
  vi.mocked(pecasApi.getPeca).mockResolvedValue({
    id: PECA_ID,
    status: 'Concluido',
    tipoPeca: 'contestacao',
    area: 'civel',
    versao: 1,
    textoGerado:
      'EXCELENTÍSSIMO SENHOR DOUTOR JUIZ.\n\nNos termos do art. 335 do CPC, contesta-se.',
    fontes: [],
    avisos: ['Confira os fatos citados.'],
    aviso: 'Rascunho: exige revisão de profissional habilitado.',
    exportacaoBloqueada: true,
    citacoes: [],
    revisadoEm: null,
    textoRevisado: null,
  })
})

describe('PecasView — formulário', () => {
  it('oferece fatos, pedidos, o comando do advogado e o anexo de documentos do processo', () => {
    renderWithProviders(<PecasView />)

    expect(screen.getByLabelText('Fatos do caso')).toBeInTheDocument()
    expect(screen.getByLabelText('Pedidos')).toBeInTheDocument()
    expect(screen.getByLabelText('Comando do advogado')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Anexar documentos/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Selecionar documentos do caso')).toBeInTheDocument()
  })

  it('não gera sem caso nem sem os fatos', async () => {
    const { user } = renderWithProviders(<PecasView />)

    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')
    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()
    expect(screen.getByText('Escolha ou abra um caso para gerar.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Anexar documentos/ })).toBeDisabled()

    await user.clear(screen.getByLabelText('Fatos do caso'))
    await escolherCaso(user)
    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()
  })
})

describe('PecasView — geração', () => {
  it('anexa ao caso escolhido e gera com o caso, os fatos, os pedidos e o comando', async () => {
    vi.mocked(pecasApi.enviarAnexo).mockResolvedValue({ id: 'doc-1', status: 'Recebido' })
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)

    const arquivo = new File(['conteudo'], 'inicial.pdf', { type: 'application/pdf' })
    vi.mocked(pecasApi.listAnexos).mockResolvedValue([anexo()])
    await user.upload(screen.getByLabelText('Selecionar documentos do caso'), arquivo)
    expect(await screen.findByText(/Pronto para uso · contrato/)).toBeInTheDocument()
    expect(pecasApi.enviarAnexo).toHaveBeenCalledWith('caso-1', arquivo)

    await user.type(screen.getByLabelText('Fatos do caso'), 'O autor foi cobrado indevidamente.')
    await user.type(screen.getByLabelText('Pedidos'), 'Improcedência total.')
    await user.type(screen.getByLabelText('Comando do advogado'), 'Priorize a preliminar.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    await waitFor(() =>
      expect(pecasApi.submeterPeca).toHaveBeenCalledWith({
        casoId: 'caso-1',
        tipoPeca: 'contestacao',
        area: 'civel',
        fatos: 'O autor foi cobrado indevidamente.',
        pedidos: 'Improcedência total.',
        instrucoesAdicionais: 'Priorize a preliminar.',
        documentosIds: undefined,
        dataFatos: undefined,
      })
    )
    expect(await screen.findByRole('article', { name: 'Minuta gerada' })).toBeInTheDocument()
    expect(screen.getByText('Confira os fatos citados.')).toBeInTheDocument()
    expect(screen.getByText(/exige revisão do advogado/)).toBeInTheDocument()
  }, 20_000)

  it('desmarcar um anexo restringe a busca aos demais', async () => {
    vi.mocked(pecasApi.listAnexos).mockResolvedValue([
      anexo(),
      anexo({ id: 'doc-2', nomeArquivo: 'outro.pdf' }),
    ])
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)
    await user.click(await screen.findByRole('checkbox', { name: 'Usar outro.pdf nesta peça' }))
    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    await waitFor(() =>
      expect(pecasApi.submeterPeca).toHaveBeenCalledWith(
        expect.objectContaining({ documentosIds: ['doc-1'] })
      )
    )
  }, 20_000)

  it('segura o botão enquanto um anexo ainda está sendo lido', async () => {
    vi.mocked(pecasApi.listAnexos).mockResolvedValue([anexo({ status: 'Convertendo' })])
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)
    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')

    expect(await screen.findByText(/Aguardando os documentos/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()
  }, 20_000)

  it('leitura de baixa confiança pede conferência com o texto extraído', async () => {
    vi.mocked(pecasApi.listAnexos).mockResolvedValue([
      anexo({ origem: 'ocr', confiancaOcr: 'baixa', requerRevisaoHumana: true }),
    ])
    vi.mocked(pecasApi.getTextoAnexo).mockResolvedValue('TEXTO EXTRAÍDO')
    vi.mocked(pecasApi.confirmarRevisaoAnexo).mockResolvedValue()
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)

    await user.click(await screen.findByRole('button', { name: 'Ver texto extraído' }))
    expect(await screen.findByText('TEXTO EXTRAÍDO')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Conferi com o original' }))

    await waitFor(() =>
      expect(pecasApi.confirmarRevisaoAnexo).toHaveBeenCalledWith('caso-1', 'doc-1')
    )
  }, 20_000)

  it('remover o anexo o apaga do caso', async () => {
    vi.mocked(pecasApi.listAnexos).mockResolvedValueOnce([anexo()])
    vi.mocked(pecasApi.removerAnexo).mockResolvedValue()
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)

    await user.click(await screen.findByRole('button', { name: 'Remover inicial.pdf' }))

    await waitFor(() => expect(screen.queryByText('inicial.pdf')).toBeNull())
    expect(pecasApi.removerAnexo).toHaveBeenCalledWith('caso-1', 'doc-1')
  }, 20_000)

  it('mostra o erro quando a geração falha e deixa tentar de novo', async () => {
    vi.mocked(pecasApi.getStatusPeca).mockResolvedValue({
      id: PECA_ID,
      status: 'Falhou',
      etapa: null,
      progresso: 0,
      mensagem: 'Falhou',
      erro: 'O serviço de IA está indisponível.',
    })
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)

    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    expect(await screen.findByText('O serviço de IA está indisponível.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(screen.queryByRole('alert')).toBeNull()
  }, 20_000)
})

describe('PecasView — revisão humana', () => {
  it('só libera a conclusão depois de decidir os itens pendentes e declarar a revisão', async () => {
    const citacao = {
      id: 'cit-1',
      texto: 'STJ, Súmula nº 999',
      status: 'PossivelAlucinacao' as const,
      detalhe: 'Não consta no contexto.',
      bloqueante: true,
      fonteUrl: null,
      decisao: 'pendente' as const,
      justificativa: null,
    }
    const base = await vi.mocked(pecasApi.getPeca)('x')
    vi.mocked(pecasApi.getPeca).mockResolvedValue({ ...base, citacoes: [citacao] })
    vi.mocked(pecasApi.decidirCitacao).mockResolvedValue({ ...citacao, decisao: 'rejeitada' })
    vi.mocked(pecasApi.concluirRevisao).mockResolvedValue()
    const { user } = renderWithProviders(<PecasView />)
    await escolherCaso(user)
    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    expect(await screen.findByText('Possível invenção da IA')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Exportar/ })).toBeNull()
    expect(screen.getByRole('button', { name: 'Rejeitar' })).toBeDisabled()
    await user.type(screen.getByLabelText('Como você conferiu'), 'Não existe no site do STJ.')
    vi.mocked(pecasApi.getPeca).mockResolvedValue({
      ...base,
      citacoes: [{ ...citacao, decisao: 'rejeitada' }],
    })
    await user.click(screen.getByRole('button', { name: 'Rejeitar' }))
    await waitFor(() => expect(screen.queryByText('Falta decidir 1 item.')).toBeNull())

    expect(screen.getByRole('button', { name: 'Concluir revisão' })).toBeDisabled()
    await user.click(screen.getByRole('checkbox', { name: /Declaro que li e conferi/ }))
    vi.mocked(pecasApi.getPeca).mockResolvedValue({
      ...base,
      exportacaoBloqueada: false,
      revisadoEm: '2026-11-22T12:00:00Z',
    })
    await user.click(screen.getByRole('button', { name: 'Concluir revisão' }))

    expect(await screen.findByRole('button', { name: /Exportar Word/ })).toBeInTheDocument()
    expect(pecasApi.concluirRevisao).toHaveBeenCalledWith(
      PECA_ID,
      expect.stringContaining('EXCELENTÍSSIMO')
    )
  }, 30_000)
})
