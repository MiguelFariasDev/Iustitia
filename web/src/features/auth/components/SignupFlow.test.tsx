import { act, screen, within } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { SignupFlow } from './SignupFlow'
import { RESEND_COOLDOWN_SECONDS } from './VerifyEmailNotice'

async function toOfficeStep(user: ReturnType<typeof renderWithProviders>['user']) {
  await fillAccount(user)
  await user.click(screen.getByRole('button', { name: 'Continuar' }))
  await screen.findByRole('heading', { name: 'Seu escritório' })
}

async function fillAccount(user: ReturnType<typeof renderWithProviders>['user'], password = 'cavalo bateria grampo certo') {
  await user.type(screen.getByLabelText('Nome completo'), 'Ana Beatriz Souza')
  await user.type(screen.getByLabelText('E-mail profissional'), 'ana@souza.adv.br')
  await user.type(screen.getByLabelText('Senha'), password)
}

describe('SignupFlow', () => {
  beforeEach(() => localStorage.clear())

  it('começa na etapa 01 e valida os campos da conta antes de avançar', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    expect(screen.getByText('01 Você')).toHaveAttribute('aria-current', 'step')

    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByText('Informe o nome completo.')).toBeInTheDocument()
    expect(screen.getByText('Informe o e-mail.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nome completo')).toHaveAttribute('aria-invalid', 'true')
  })

  it('exige 12+ caracteres, mas não exige símbolo (frase-senha vale)', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    await fillAccount(user, 'curta123')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText(/pelo menos 12 caracteres/i)).toBeInTheDocument()

    await user.clear(screen.getByLabelText('Senha'))
    await user.type(screen.getByLabelText('Senha'), 'cavalo bateria grampo certo')
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    await screen.findByRole('heading', { name: 'Seu escritório' })
    expect(screen.getByText('02 Escritório')).toHaveAttribute('aria-current', 'step')
  })

  it('mostra o medidor de força em texto', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    await user.type(screen.getByLabelText('Senha'), 'cavalo bateria grampo certo')
    expect(screen.getByText(/Força da senha/)).toHaveTextContent('Forte')
  })

  it('não perde o que foi digitado ao voltar da etapa 2 para a 1', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    await toOfficeStep(user)

    await user.click(screen.getByRole('button', { name: 'Voltar' }))

    expect(await screen.findByLabelText('Nome completo')).toHaveValue('Ana Beatriz Souza')
    expect(screen.getByLabelText('E-mail profissional')).toHaveValue('ana@souza.adv.br')
  })

  it('na etapa 2 valida CNPJ, OAB e o aceite dos termos', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    await toOfficeStep(user)

    await user.type(screen.getByLabelText('CNPJ do escritório'), '11111111111111')
    await user.click(screen.getByRole('button', { name: 'Criar escritório' }))

    expect(await screen.findByText('CNPJ inválido.')).toBeInTheDocument()
    expect(screen.getByText('Informe o número da sua OAB.')).toBeInTheDocument()
    expect(screen.getByText(/aceitar os Termos de Uso/)).toBeInTheDocument()
    expect(screen.getByText('OAB informada, não verificada.')).toBeInTheDocument()
  })

  it('os links de termos e privacidade são reais', async () => {
    const { user } = renderWithProviders(<SignupFlow />)
    await toOfficeStep(user)

    expect(screen.getByRole('link', { name: 'Termos de Uso' })).toHaveAttribute('href', '/termos')
    expect(screen.getByRole('link', { name: 'Política de Privacidade' })).toHaveAttribute('href', '/privacidade')
  })
})

describe('VerifyEmailNotice', () => {
  it('exibe o texto neutro (não revela se o e-mail já existia) e contagem para reenviar', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    const { VerifyEmailNotice } = await import('./VerifyEmailNotice')
    renderWithProviders(<VerifyEmailNotice email="ana@souza.adv.br" onChangeEmail={() => undefined} />)

    expect(screen.getByRole('status')).toHaveTextContent(/Se ana@souza.adv.br for um e-mail novo/)
    const resend = screen.getByRole('button', { name: new RegExp(`Reenviar em ${RESEND_COOLDOWN_SECONDS}s`) })
    expect(resend).toBeDisabled()

    // A contagem é uma cadeia de timers de 1 s (um por render): avança um segundo por vez.
    for (let i = 0; i <= RESEND_COOLDOWN_SECONDS; i++) {
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000)
      })
    }
    expect(within(document.body).getByRole('button', { name: 'Reenviar e-mail' })).toBeEnabled()
    vi.useRealTimers()
  })
})
