import { screen, waitFor } from '@testing-library/react'
import * as processosApi from '@/features/processos/api'
import { renderWithProviders } from '@/test/render'
import * as pecasApi from '../api'
import { PecasView } from './PecasView'

vi.mock('@/features/processos/api')
vi.mock('../api')
vi.mock('@/lib/api/toastHandler', () => ({ handleApiError: vi.fn() }))
vi.mock('@/lib/notify', () => ({
  notify: { success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() },
}))

const PECA_ID = '11111111-1111-1111-1111-111111111111'

beforeEach(() => {
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
    exportacaoBloqueada: false,
  })
})

describe('PecasView — formulário', () => {
  it('oferece fatos, pedidos, o comando do advogado e o anexo de documentos do processo', () => {
    renderWithProviders(<PecasView />)

    expect(screen.getByLabelText('Fatos do caso')).toBeInTheDocument()
    expect(screen.getByLabelText('Pedidos')).toBeInTheDocument()
    expect(screen.getByLabelText('Comando do advogado')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Anexar documentos/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Selecionar documentos do processo')).toBeInTheDocument()
  })

  it('não gera sem os fatos', () => {
    renderWithProviders(<PecasView />)

    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()
  })
})

describe('PecasView — geração', () => {
  it('envia fatos, pedidos, comando e os documentos anexados já indexados', async () => {
    vi.mocked(pecasApi.enviarAnexo).mockResolvedValue({ id: 'doc-1', status: 'Indexado' })
    const { user } = renderWithProviders(<PecasView />)

    const arquivo = new File(['conteudo'], 'inicial.pdf', { type: 'application/pdf' })
    await user.upload(screen.getByLabelText('Selecionar documentos do processo'), arquivo)
    expect(await screen.findByText('Pronto para uso')).toBeInTheDocument()
    expect(pecasApi.enviarAnexo).toHaveBeenCalledWith(arquivo, 'peticao')

    await user.type(screen.getByLabelText('Fatos do caso'), 'O autor foi cobrado indevidamente.')
    await user.type(screen.getByLabelText('Pedidos'), 'Improcedência total.')
    await user.type(screen.getByLabelText('Comando do advogado'), 'Priorize a preliminar.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    await waitFor(() =>
      expect(pecasApi.submeterPeca).toHaveBeenCalledWith({
        tipoPeca: 'contestacao',
        area: 'civel',
        fatos: 'O autor foi cobrado indevidamente.',
        pedidos: 'Improcedência total.',
        instrucoesAdicionais: 'Priorize a preliminar.',
        documentosIds: ['doc-1'],
      })
    )
    expect(await screen.findByRole('article', { name: 'Minuta gerada' })).toBeInTheDocument()
    expect(screen.getByText(/EXCELENTÍSSIMO SENHOR DOUTOR JUIZ/)).toBeInTheDocument()
    expect(screen.getByText('Confira os fatos citados.')).toBeInTheDocument()
    expect(screen.getByText(/exige revisão do advogado/)).toBeInTheDocument()
  }, 20_000)

  it('segura o botão enquanto um anexo ainda está sendo indexado', async () => {
    vi.mocked(pecasApi.enviarAnexo).mockResolvedValue({ id: 'doc-2', status: 'Pendente' })
    const { user } = renderWithProviders(<PecasView />)

    await user.upload(
      screen.getByLabelText('Selecionar documentos do processo'),
      new File(['x'], 'contrato.docx')
    )
    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')

    expect(await screen.findByText(/Aguardando os documentos/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Gerar minuta com IA/ })).toBeDisabled()
  })

  it('remover o anexo o tira do acervo e da lista', async () => {
    vi.mocked(pecasApi.enviarAnexo).mockResolvedValue({ id: 'doc-3', status: 'Indexado' })
    vi.mocked(pecasApi.removerAnexo).mockResolvedValue()
    const { user } = renderWithProviders(<PecasView />)

    await user.upload(
      screen.getByLabelText('Selecionar documentos do processo'),
      new File(['x'], 'parecer.txt')
    )
    await user.click(await screen.findByRole('button', { name: 'Remover parecer.txt' }))

    expect(screen.queryByText('parecer.txt')).toBeNull()
    expect(pecasApi.removerAnexo).toHaveBeenCalledWith('doc-3')
  })

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

    await user.type(screen.getByLabelText('Fatos do caso'), 'Fatos.')
    await user.click(screen.getByRole('button', { name: /Gerar minuta com IA/ }))

    expect(await screen.findByText('O serviço de IA está indisponível.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }))
    expect(screen.queryByRole('alert')).toBeNull()
  })
})
