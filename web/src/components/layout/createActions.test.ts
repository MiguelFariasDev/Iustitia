import { createActions } from './createActions'

describe('createActions', () => {
  it('processo, cliente, tarefa, evento e peça levam a uma tela de verdade', () => {
    const hrefs = Object.fromEntries(createActions.map((a) => [a.id, a.href]))

    expect(hrefs.process).toBe('/processos/novo')
    expect(hrefs.client).toBe('/clientes/novo')
    expect(hrefs.task).toBe('/tarefas?nova=1')
    expect(hrefs.event).toBe('/agenda?novo=evento')
    expect(hrefs.deadline).toBe('/agenda?novo=prazo')
    expect(hrefs.hearing).toBe('/agenda?novo=audiencia')
    expect(hrefs.brief).toBe('/criacao-de-pecas')
  })
})

describe('Novo › Neste processo', () => {
  it('reconhece só a tela de um processo', async () => {
    const { processoDaRota, hrefNoProcesso } = await import('./createActions')
    const id = '3fb47f03-1ae0-40e1-a69c-2fc1c0af8884'

    expect(processoDaRota(`/processos/${id}`)).toBe(id)
    expect(processoDaRota(`/processos/${id}/editar`)).toBeNull()
    expect(processoDaRota('/processos/novo')).toBeNull()
    expect(processoDaRota('/tarefas')).toBeNull()
    expect(hrefNoProcesso(id, 'audiencia')).toBe(`/processos/${id}?novo=audiencia`)
  })
})
