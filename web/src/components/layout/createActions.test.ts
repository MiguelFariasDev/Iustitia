import { createActions } from './createActions'

describe('createActions', () => {
  it('processo, cliente, tarefa, evento e peça levam a uma tela de verdade', () => {
    const hrefs = Object.fromEntries(createActions.map((a) => [a.id, a.href]))

    expect(hrefs.process).toBe('/processos/novo')
    expect(hrefs.client).toBe('/clientes/novo')
    expect(hrefs.task).toBe('/tarefas?nova=1')
    expect(hrefs.event).toBe('/agenda?novo=1')
    expect(hrefs.brief).toBe('/criacao-de-pecas')
  })
})
