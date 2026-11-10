import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { mockListEventos, resetAgendaMock } from '../mocks'
import { AgendaView } from './AgendaView'

beforeEach(() => resetAgendaMock())

async function renderAgenda() {
  const utils = renderWithProviders(<AgendaView />)
  await screen.findByRole('button', { name: /Audiência: Audiência de instrução/ })
  return utils
}

describe('AgendaView', () => {
  it('mostra os eventos do mês com o tipo no nome acessível', async () => {
    await renderAgenda()

    expect(screen.getByRole('grid', { name: 'Calendário do mês' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Prazo: Prazo: contestação/ })).toBeInTheDocument()
  })

  it('com um tipo filtrado, só o ponto dos demais esmaece; o texto continua legível', async () => {
    const { user } = await renderAgenda()
    const filtro = screen.getByRole('group', { name: 'Filtrar por tipo' })
    const ponto = (nome: RegExp) =>
      within(filtro).getByRole('button', { name: nome }).querySelector('span[aria-hidden] ')!

    expect(ponto(/Reunião/)).not.toHaveClass('opacity-50')

    await user.click(within(filtro).getByRole('button', { name: /Reunião/ }))

    expect(ponto(/Reunião/)).not.toHaveClass('opacity-50')
    expect(ponto(/Audiência/)).toHaveClass('opacity-50')
    expect(within(filtro).getByRole('button', { name: /Audiência/ })).not.toHaveClass('opacity-50')
  })

  it('filtra por tipo pelo chip', async () => {
    const { user } = await renderAgenda()
    const filtro = screen.getByRole('group', { name: 'Filtrar por tipo' })

    await user.click(within(filtro).getByRole('button', { name: /Reunião/ }))

    expect(screen.queryByRole('button', { name: /Audiência: Audiência de instrução/ })).toBeNull()
    expect(screen.getByRole('button', { name: /Reunião: Reunião com cliente/ })).toBeInTheDocument()
  })

  it('filtra a agenda por responsável', async () => {
    const { user } = await renderAgenda()

    await user.click(screen.getByRole('combobox', { name: 'Filtrar por responsável' }))
    await user.click(await screen.findByRole('option', { name: /^Bruno Lima/ }))

    // Os dois eventos do mock atribuídos a u-2 permanecem; os demais saem.
    await waitFor(() =>
      expect(screen.queryByRole('button', { name: /Audiência: Audiência de instrução/ })).toBeNull()
    )
    expect(
      screen.getByRole('button', { name: /Tarefa: Revisar minuta de acordo/ })
    ).toBeInTheDocument()
  })

  it('troca para a visão semanal', async () => {
    const { user } = await renderAgenda()

    await user.click(screen.getByRole('radio', { name: 'Semana' }))

    expect(await screen.findByRole('grid', { name: 'Agenda por horário' })).toBeInTheDocument()
  })

  it('cria um evento pelo diálogo', async () => {
    const { user } = await renderAgenda()
    const antes = mockListEventos({ from: '2000-01-01', to: '2100-01-01' }).length

    await user.click(screen.getByRole('button', { name: 'Novo evento' }))
    const dialog = await screen.findByRole('dialog', { name: 'Novo evento' })
    await user.type(within(dialog).getByLabelText('Título'), 'Reunião de alinhamento')
    await user.click(within(dialog).getByRole('button', { name: 'Criar evento' }))

    await waitFor(() =>
      expect(mockListEventos({ from: '2000-01-01', to: '2100-01-01' })).toHaveLength(antes + 1)
    )
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('valida o título antes de enviar', async () => {
    const { user } = await renderAgenda()

    await user.click(screen.getByRole('button', { name: 'Novo evento' }))
    const dialog = await screen.findByRole('dialog', { name: 'Novo evento' })
    await user.click(within(dialog).getByRole('button', { name: 'Criar evento' }))

    expect(await within(dialog).findByText('Informe o título do evento.')).toBeInTheDocument()
  })

  it('arrastar um evento para outro dia remarca no servidor', async () => {
    await renderAgenda()
    const chip = screen.getByRole('button', { name: /Audiência: Audiência de instrução/ })
    const antes = mockListEventos({ from: '2000-01-01', to: '2100-01-01' }).find(
      (e) => e.titulo === 'Audiência de instrução'
    )!
    const celulas = screen.getAllByRole('gridcell')
    const origem = chip.closest('[role="gridcell"]')!
    const destino = celulas[celulas.indexOf(origem as HTMLElement) + 2]!
    const dataTransfer = { data: {} as Record<string, string>, effectAllowed: '' }
    const dt = {
      setData: (k: string, v: string) => (dataTransfer.data[k] = v),
      getData: (k: string) => dataTransfer.data[k] ?? '',
      set effectAllowed(v: string) {
        dataTransfer.effectAllowed = v
      },
      get effectAllowed() {
        return dataTransfer.effectAllowed
      },
    }

    fireEvent.dragStart(chip, { dataTransfer: dt })
    fireEvent.dragOver(destino, { dataTransfer: dt })
    fireEvent.drop(destino, { dataTransfer: dt })

    await waitFor(() => {
      const depois = mockListEventos({ from: '2000-01-01', to: '2100-01-01' }).find(
        (e) => e.id === antes.id
      )!
      expect(depois.inicio).not.toBe(antes.inicio)
    })
  })
})
