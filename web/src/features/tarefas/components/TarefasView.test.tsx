import { fireEvent, screen, within } from '@testing-library/react'
import { useNow } from '@/hooks/useNow'
import { renderWithProviders } from '@/test/render'
import { useTarefasStore } from '../store'
import { TarefasView } from './TarefasView'

vi.mock('@/hooks/useNow', () => ({ useNow: vi.fn() }))

beforeEach(() => {
  vi.mocked(useNow).mockReturnValue(new Date())
  useTarefasStore.getState().limpar()
})

afterEach(() => vi.resetAllMocks())

const coluna = (nome: RegExp) => screen.getByRole('region', { name: nome })

describe('TarefasView', () => {
  it('mostra as quatro colunas, com a contagem de cada uma', () => {
    renderWithProviders(<TarefasView />)

    expect(coluna(/^A fazer, 2 tarefas/)).toBeInTheDocument()
    expect(coluna(/^Em andamento, 2 tarefas/)).toBeInTheDocument()
    expect(coluna(/^Em revisão, 2 tarefas/)).toBeInTheDocument()
    expect(coluna(/^Concluído, 2 tarefas/)).toBeInTheDocument()
  })

  it('avisa que é demonstração (não existe módulo de tarefas)', () => {
    renderWithProviders(<TarefasView />)

    expect(screen.getByRole('note')).toHaveTextContent('Dados de demonstração')
  })

  it('o cartão traz título, processo em mono, responsável e o anel de prazo', () => {
    renderWithProviders(<TarefasView />)
    const aFazer = coluna(/^A fazer/)
    const cartao = within(aFazer).getByText('Protocolar contestação').closest('article')!

    expect(within(cartao).getByText('0000777-07.2018.8.06.0100')).toHaveClass('font-mono')
    expect(within(cartao).getByText('Marina Lopes')).toBeInTheDocument()
    expect(
      within(cartao).getByRole('img', { name: /Prazo: \dd, \d+% do prazo decorrido/ })
    ).toBeInTheDocument()
  })

  it('tarefa concluída mostra "ok" no anel, tenha ou não prazo', () => {
    renderWithProviders(<TarefasView />)
    const concluido = within(coluna(/^Concluído/))

    for (const titulo of ['Enviar documentos ao cliente', 'Organizar pasta do processo']) {
      const cartao = within(concluido.getByText(titulo).closest('article')!)

      expect(cartao.getByRole('img', { name: /Prazo: ok, 100%/ })).toBeInTheDocument()
    }
  })

  it('prazo hoje já é crítico (vermelho) e tem o rótulo "0d"', () => {
    renderWithProviders(<TarefasView />)
    const cartao = within(coluna(/^Em andamento/))
      .getByText('Preparar réplica')
      .closest('article')!
    const anel = within(cartao).getByRole('img')

    expect(anel).toHaveAccessibleName(/Prazo: 0d, 100%/)
    expect(anel.querySelectorAll('circle')[1]).toHaveClass('stroke-destructive')
  })

  it('‹ e › movem o cartão entre colunas e avisam o leitor de tela', async () => {
    const { user } = renderWithProviders(<TarefasView />)

    await user.click(
      screen.getByRole('button', { name: 'Mover “Protocolar contestação” para Em andamento' })
    )

    expect(within(coluna(/^A fazer/)).queryByText('Protocolar contestação')).toBeNull()
    expect(within(coluna(/^Em andamento/)).getByText('Protocolar contestação')).toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent(
      '“Protocolar contestação” movida para Em andamento.'
    )
  })

  it('nas pontas o botão fica desabilitado (primeira e última coluna)', () => {
    renderWithProviders(<TarefasView />)
    const primeira = within(
      within(coluna(/^A fazer/))
        .getByText('Protocolar contestação')
        .closest('article')!
    )
    const ultima = within(
      within(coluna(/^Concluído/))
        .getByText('Enviar documentos ao cliente')
        .closest('article')!
    )

    expect(primeira.getByRole('button', { name: 'Já está na primeira coluna' })).toBeDisabled()
    expect(ultima.getByRole('button', { name: 'Já está na última coluna' })).toBeDisabled()
    expect(primeira.getByRole('button', { name: /Mover .* para Em andamento/ })).toBeEnabled()
  })

  it('arrastar para outra coluna move o cartão', () => {
    renderWithProviders(<TarefasView />)
    const cartao = within(coluna(/^A fazer/))
      .getByText('Juntar procuração')
      .closest('article')!
    const dados: Record<string, string> = {}
    const dt = {
      setData: (k: string, v: string) => (dados[k] = v),
      getData: (k: string) => dados[k] ?? '',
      effectAllowed: '',
    }

    fireEvent.dragStart(cartao, { dataTransfer: dt })
    fireEvent.dragOver(coluna(/^Em revisão/), { dataTransfer: dt })
    fireEvent.drop(coluna(/^Em revisão/), { dataTransfer: dt })

    expect(within(coluna(/^Em revisão/)).getByText('Juntar procuração')).toBeInTheDocument()
    expect(within(coluna(/^A fazer/)).queryByText('Juntar procuração')).toBeNull()
  })

  it('soltar na mesma coluna não faz nada (nem avisa)', () => {
    renderWithProviders(<TarefasView />)
    const cartao = within(coluna(/^A fazer/))
      .getByText('Juntar procuração')
      .closest('article')!
    const dt = { setData: vi.fn(), getData: () => 't2', effectAllowed: '' }

    fireEvent.dragStart(cartao, { dataTransfer: dt })
    fireEvent.drop(coluna(/^A fazer/), { dataTransfer: dt })

    expect(screen.getByRole('status')).toBeEmptyDOMElement()
  })

  it('esvaziar uma coluna mostra o estado vazio', async () => {
    const { user } = renderWithProviders(<TarefasView />)

    for (const titulo of ['Enviar documentos ao cliente', 'Organizar pasta do processo']) {
      const botao = screen.getByRole('button', { name: `Mover “${titulo}” para Em revisão` })
      await user.click(botao)
    }

    expect(within(coluna(/^Concluído/)).getByText('Nenhuma tarefa.')).toBeInTheDocument()
  })

  it('antes de hidratar (sem "agora") o anel não chuta um prazo', () => {
    vi.mocked(useNow).mockReturnValue(null)
    renderWithProviders(<TarefasView />)

    expect(screen.getAllByRole('img', { name: /Prazo: …, 0%/ }).length).toBeGreaterThan(0)
  })
})
