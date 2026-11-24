const toastMock = vi.hoisted(() => ({
  success: vi.fn(),
  error: vi.fn(),
  warning: vi.fn(),
  info: vi.fn(),
}))
vi.mock('sonner', () => ({ toast: toastMock }))

import { notify } from './notify'

afterEach(() => vi.clearAllMocks())

describe('notify — duração do toast', () => {
  it('sucesso e informação somem em 2,4 s, como no protótipo', () => {
    notify.success('Aprovado')
    notify.info('Atualizado')

    expect(toastMock.success).toHaveBeenCalledWith(
      'Aprovado',
      expect.objectContaining({ duration: 2400 })
    )
    expect(toastMock.info).toHaveBeenCalledWith(
      'Atualizado',
      expect.objectContaining({ duration: 2400 })
    )
  })

  it('erro fica mais tempo: 2,4 s não dá para ler uma mensagem de falha', () => {
    notify.error('Falha ao salvar')

    expect(toastMock.error).toHaveBeenCalledWith(
      'Falha ao salvar',
      expect.objectContaining({ duration: 6000 })
    )
  })

  it('toast com ação (ex.: Desfazer) também fica mais tempo', () => {
    notify.success('Excluído', { action: { label: 'Desfazer', onClick: () => undefined } })

    expect(toastMock.success).toHaveBeenCalledWith(
      'Excluído',
      expect.objectContaining({ duration: 6000 })
    )
  })
})
