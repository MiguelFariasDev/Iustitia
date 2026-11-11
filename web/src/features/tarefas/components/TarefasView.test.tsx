import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { useNow } from '@/hooks/useNow'
import { navigationState } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { criarTarefa, limparTarefas } from '../store'
import { TarefasView } from './TarefasView'

vi.mock('@/hooks/useNow', () => ({ useNow: vi.fn() }))

beforeEach(() => {
  vi.mocked(useNow).mockReturnValue(new Date())
  limparTarefas()
})

afterEach(() => vi.resetAllMocks())

const coluna = (nome: RegExp) => screen.getByRole('region', { name: nome })

describe('TarefasView', () => {
  it('começa vazia, sem tarefas de exemplo', async () => {
    renderWithProviders(<TarefasView />)

    expect(await screen.findByRole('region', { name: /^A fazer, 0 tarefas/ })).toBeInTheDocument()
    expect(coluna(/^Concluído, 0 tarefas/)).toBeInTheDocument()
    expect(screen.getAllByText('Nenhuma tarefa.')).toHaveLength(4)
  })

  it('avisa que as tarefas são do escritório', () => {
    renderWithProviders(<TarefasView />)

    expect(screen.getByRole('note')).toHaveTextContent('são do escritório')
  })

  it('mostra a tarefa criada com título, processo em mono e prazo, e permite mover e excluir', async () => {
    act(() => {
      criarTarefa({
        titulo: 'Protocolar contestação',
        prazo: '2099-01-10',
        processo: { id: 'p1', cnjNumber: '0000777-07.2018.8.06.0100' },
        responsavel: { id: 'u1', nome: 'Marina Lopes' },
      })
    })
    renderWithProviders(<TarefasView />)
    const secao = await screen.findByRole('region', { name: /^A fazer, 1 tarefas/ })
    const cartao = within(secao).getByText('Protocolar contestação').closest('article')!

    expect(within(cartao).getByText('0000777-07.2018.8.06.0100')).toHaveClass('font-mono')
    expect(within(cartao).getByText('Marina Lopes')).toBeInTheDocument()

    fireEvent.click(
      within(cartao).getByRole('button', { name: /Mover “Protocolar contestação” para Em andamento/ })
    )
    expect(await screen.findByRole('region', { name: /^Em andamento, 1 tarefas/ })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: /Excluir “Protocolar contestação”/ }))
    await waitFor(() => expect(screen.queryByText('Protocolar contestação')).not.toBeInTheDocument())
  })

  it('cria uma tarefa pelo botão "Nova tarefa"', async () => {
    renderWithProviders(<TarefasView />)
    await screen.findByRole('region', { name: /^A fazer, 0 tarefas/ })

    fireEvent.click(screen.getByRole('button', { name: /Nova tarefa/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Criar tarefa' }))
    expect(screen.getByRole('alert')).toHaveTextContent('Informe o título')

    fireEvent.change(screen.getByLabelText('Título'), { target: { value: 'Juntar procuração' } })
    fireEvent.click(screen.getByRole('button', { name: 'Criar tarefa' }))

    const secao = await screen.findByRole('region', { name: /^A fazer, 1 tarefas/ })
    expect(within(secao).getByText('Juntar procuração')).toBeInTheDocument()
  })

  it('abre o formulário direto quando chega de "Novo > Nova tarefa" (?nova=1)', () => {
    navigationState.searchParams = new URLSearchParams('nova=1')
    renderWithProviders(<TarefasView />)

    expect(screen.getByRole('dialog', { name: 'Nova tarefa' })).toBeInTheDocument()
  })
})
