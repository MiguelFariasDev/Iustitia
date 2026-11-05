import { screen, waitFor, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { useAuthStore } from '@/stores/authStore'
import { resetMockAtendimentos } from '../mocks'
import { ChatPanel } from './ChatPanel'

const ME = 'adv-1'

beforeEach(() => {
  resetMockAtendimentos()
  useAuthStore.getState().setSession({
    accessToken: 't',
    expiresAt: null,
    user: {
      id: ME,
      email: 'a@b.c',
      name: 'Ana',
      avatarUrl: null,
      role: 'Lawyer',
      tenantId: 'tenant-1',
    },
  })
})

afterEach(() => useAuthStore.getState().clear())

const composer = () => screen.getByLabelText('Mensagem para o cliente')

/** atd-001 Aguardando · atd-003 Humano (outro advogado) · atd-004 Bot · atd-005 Encerrado */
async function openChat(id: string) {
  const view = renderWithProviders(<ChatPanel atendimentoId={id} />)
  await screen.findByRole('log', { name: 'Mensagens da conversa' })
  await screen.findByRole('heading', { level: 2 })
  return view
}

describe('ChatPanel — o campo de digitação só libera para quem assumiu', () => {
  it('Aguardando: bloqueado, com o motivo dito em voz alta e o botão "Assumir atendimento"', async () => {
    await openChat('atd-001')

    expect(composer()).toBeDisabled()
    expect(screen.getByText('Assuma o atendimento para responder ao cliente.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Assumir atendimento' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Enviar mensagem' })).toBeDisabled()
  })

  it('Bot: bloqueado e SEM botão de assumir (assumir é para quem está Aguardando)', async () => {
    await openChat('atd-004')

    expect(composer()).toBeDisabled()
    expect(screen.getByText('O bot está conduzindo esta conversa.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assumir atendimento' })).not.toBeInTheDocument()
  })

  it('Humano de OUTRO advogado: bloqueado, sem assumir nem devolver', async () => {
    await openChat('atd-003')

    expect(composer()).toBeDisabled()
    expect(screen.getByText('Esta conversa está com outro advogado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Devolver ao bot' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assumir atendimento' })).not.toBeInTheDocument()
  })

  it('Encerrado: tudo bloqueado, sem ações', async () => {
    await openChat('atd-005')

    expect(composer()).toBeDisabled()
    expect(screen.getByText('Atendimento encerrado.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Encerrar atendimento' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assumir atendimento' })).not.toBeInTheDocument()
  })

  it('assumir libera o campo, mostra "Devolver ao bot" e tira o botão de assumir', async () => {
    const { user } = await openChat('atd-001')

    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))

    await waitFor(() => expect(composer()).toBeEnabled())
    expect(screen.getByRole('button', { name: 'Devolver ao bot' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Assumir atendimento' })).not.toBeInTheDocument()
    expect(screen.getByText('Humano')).toBeInTheDocument()
  })
})

describe('ChatPanel — conversa', () => {
  it('mostra quem disse o quê, em texto, e preserva as quebras de linha do menu do bot', async () => {
    await openChat('atd-001')

    const log = screen.getByRole('log', { name: 'Mensagens da conversa' })
    expect((await within(log).findAllByText('Cliente')).length).toBeGreaterThan(0)
    expect(within(log).getAllByText('Bot').length).toBeGreaterThan(0)
    expect(within(log).getByText('Aguarde, um advogado vai te atender.')).toBeInTheDocument()
    expect(within(log).getByText(/Digite:/).className).toContain('whitespace-pre-wrap')
  })

  it('mensagem com HTML aparece como TEXTO, nunca é interpretada', async () => {
    const { user } = await openChat('atd-001')
    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))
    await waitFor(() => expect(composer()).toBeEnabled())

    await user.type(composer(), '<img src=x onerror=alert(1)>')
    await user.click(screen.getByRole('button', { name: 'Enviar mensagem' }))

    const log = screen.getByRole('log', { name: 'Mensagens da conversa' })
    expect(await within(log).findByText('<img src=x onerror=alert(1)>')).toBeInTheDocument()
    expect(log.querySelector('img')).toBeNull()
  })

  it('envia com o botão: a mensagem aparece na conversa e o rascunho é limpo', async () => {
    const { user } = await openChat('atd-001')
    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))
    await waitFor(() => expect(composer()).toBeEnabled())

    await user.type(composer(), 'Bom dia, Maria.')
    await user.click(screen.getByRole('button', { name: 'Enviar mensagem' }))

    const log = screen.getByRole('log', { name: 'Mensagens da conversa' })
    expect(await within(log).findByText('Bom dia, Maria.')).toBeInTheDocument()
    expect(composer()).toHaveValue('')
  })

  it('Enter envia e Shift+Enter quebra a linha', async () => {
    const { user } = await openChat('atd-001')
    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))
    await waitFor(() => expect(composer()).toBeEnabled())

    await user.type(composer(), 'linha 1{Shift>}{Enter}{/Shift}linha 2')
    expect(composer()).toHaveValue('linha 1\nlinha 2')

    await user.keyboard('{Enter}')

    const log = screen.getByRole('log', { name: 'Mensagens da conversa' })
    expect(await within(log).findByText(/linha 1/)).toBeInTheDocument()
    expect(composer()).toHaveValue('')
  })

  it('não envia mensagem em branco', async () => {
    const { user } = await openChat('atd-001')
    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))
    await waitFor(() => expect(composer()).toBeEnabled())

    await user.type(composer(), '   ')

    expect(screen.getByRole('button', { name: 'Enviar mensagem' })).toBeDisabled()
  })

  it('o campo respeita o limite de 4096 caracteres do WhatsApp', async () => {
    await openChat('atd-001')

    expect(composer()).toHaveAttribute('maxlength', '4096')
  })

  it('devolver ao bot bloqueia o campo de novo', async () => {
    const { user } = await openChat('atd-001')
    await user.click(screen.getByRole('button', { name: 'Assumir atendimento' }))
    await waitFor(() => expect(composer()).toBeEnabled())

    // O botão fica desabilitado enquanto a mutação anterior termina de recarregar os dados.
    const devolver = await screen.findByRole('button', { name: 'Devolver ao bot' })
    await waitFor(() => expect(devolver).toBeEnabled())
    await user.click(devolver)

    await waitFor(() => expect(composer()).toBeDisabled())
    expect(screen.getByText('O bot está conduzindo esta conversa.')).toBeInTheDocument()
  })
})

describe('ChatPanel — encerrar', () => {
  it('pede confirmação antes de encerrar, e cancelar não muda nada', async () => {
    const { user } = await openChat('atd-001')

    await user.click(screen.getByRole('button', { name: 'Encerrar atendimento' }))
    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Encerrar este atendimento?')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(screen.getByText('Aguardando')).toBeInTheDocument()
  })

  it('confirmar encerra: o campo bloqueia e as ações somem', async () => {
    const { user } = await openChat('atd-001')

    await user.click(screen.getByRole('button', { name: 'Encerrar atendimento' }))
    const dialog = await screen.findByRole('alertdialog')
    await user.click(within(dialog).getByRole('button', { name: 'Encerrar atendimento' }))

    await waitFor(() => expect(screen.getByText('Atendimento encerrado.')).toBeInTheDocument())
    expect(composer()).toBeDisabled()
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument())
    expect(screen.queryByRole('button', { name: 'Encerrar atendimento' })).not.toBeInTheDocument()
  })
})

describe('ChatPanel — erro', () => {
  it('atendimento inexistente mostra o estado de erro, não uma tela quebrada', async () => {
    renderWithProviders(<ChatPanel atendimentoId="nao-existe" />)

    expect(await screen.findByText('Não foi possível carregar o atendimento')).toBeInTheDocument()
  })
})
