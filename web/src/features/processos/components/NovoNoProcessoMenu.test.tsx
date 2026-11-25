import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { navigationState, routerMock } from '@/test/navigation'
import { renderWithProviders } from '@/test/render'
import { NovoNoProcessoMenu, presetDoEvento } from './NovoNoProcessoMenu'

const ID = '3fb47f03-1ae0-40e1-a69c-2fc1c0af8884'

function render() {
  return renderWithProviders(
    <NovoNoProcessoMenu processoId={ID} cnjNumber="0000777-07.2018.8.06.0100" clienteNome="Maria" />
  )
}

describe('NovoNoProcessoMenu', () => {
  it('oferece tarefa, evento, prazo, audiência, atendimento e documento', async () => {
    const user = userEvent.setup()
    render()

    await user.click(screen.getByRole('button', { name: 'Criar novo item neste processo' }))

    for (const nome of ['Tarefa', 'Evento', 'Prazo', 'Audiência', 'Atendimento', 'Documento']) {
      expect(screen.getByRole('menuitem', { name: nome })).toBeInTheDocument()
    }
  })

  it('?novo=audiencia (vindo do "Novo" da barra superior) abre o diálogo e limpa a URL ao fechar', async () => {
    const user = userEvent.setup()
    navigationState.pathname = `/processos/${ID}`
    navigationState.searchParams = new URLSearchParams('novo=audiencia')
    render()

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(routerMock.replace).toHaveBeenCalledWith(`/processos/${ID}`, { scroll: false })
  })

  it('cada opção já nasce com o tipo certo e ligada ao processo', () => {
    expect(presetDoEvento('prazo', ID, null)).toEqual({ processoId: ID, tipo: 'Prazo', diaInteiro: true })
    expect(presetDoEvento('audiencia', ID, null)).toEqual({ processoId: ID, tipo: 'Audiencia' })
    expect(presetDoEvento('atendimento', ID, 'Maria')).toMatchObject({
      processoId: ID,
      tipo: 'Reuniao',
      titulo: 'Atendimento — Maria',
    })
  })
})
