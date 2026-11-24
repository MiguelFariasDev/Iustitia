import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthError } from '@/lib/auth/types'

const mocks = vi.hoisted(() => ({
  mfaStatus: vi.fn(),
  mfaEnroll: vi.fn(),
  mfaVerify: vi.fn(),
  assign: vi.fn(),
}))

vi.mock('@/lib/auth/authService', () => ({
  authService: { mfaStatus: mocks.mfaStatus, mfaEnroll: mocks.mfaEnroll, mfaVerify: mocks.mfaVerify },
}))
vi.mock('next/navigation', () => ({
  useRouter: () => ({ refresh: vi.fn() }),
  useSearchParams: () => new URLSearchParams('next=/processos'),
}))
vi.mock('@/lib/observability/reportError', () => ({ reportError: vi.fn() }))

import { MfaForm } from './MfaForm'

describe('MfaForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    Object.defineProperty(window, 'location', {
      value: { ...window.location, assign: mocks.assign },
      writable: true,
    })
  })

  it('sem autenticador cadastrado, mostra o QR code e a chave', async () => {
    mocks.mfaStatus.mockResolvedValue({ enrolled: false, factorId: null })
    mocks.mfaEnroll.mockResolvedValue({ factorId: 'f1', qrCode: 'data:image/svg+xml;utf-8,<svg/>', secret: 'ABC123' })

    render(<MfaForm />)

    expect(await screen.findByAltText(/QR code/i)).toBeInTheDocument()
    expect(screen.getByText('ABC123')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /ativar e continuar/i })).toBeDisabled()
  })

  it('com autenticador cadastrado, só pede o código e volta para a rota de origem', async () => {
    mocks.mfaStatus.mockResolvedValue({ enrolled: true, factorId: 'f9' })
    mocks.mfaVerify.mockResolvedValue(undefined)
    const user = userEvent.setup()

    render(<MfaForm />)
    await user.type(await screen.findByLabelText(/código de 6 dígitos/i), '123456')
    await user.click(screen.getByRole('button', { name: /verificar/i }))

    await waitFor(() => expect(mocks.mfaVerify).toHaveBeenCalledWith('f9', '123456'))
    expect(mocks.mfaEnroll).not.toHaveBeenCalled()
    expect(mocks.assign).toHaveBeenCalledWith('/processos')
  })

  it('mostra o erro e limpa o campo quando o código está errado', async () => {
    mocks.mfaStatus.mockResolvedValue({ enrolled: true, factorId: 'f9' })
    mocks.mfaVerify.mockRejectedValue(new AuthError('Código inválido ou expirado.', 'mfa_verification_failed'))
    const user = userEvent.setup()

    render(<MfaForm />)
    const field = await screen.findByLabelText(/código de 6 dígitos/i)
    await user.type(field, '000000')
    await user.click(screen.getByRole('button', { name: /verificar/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/inválido/i)
    expect(field).toHaveValue('')
    expect(mocks.assign).not.toHaveBeenCalled()
  })

  it('só aceita dígitos no campo', async () => {
    mocks.mfaStatus.mockResolvedValue({ enrolled: true, factorId: 'f9' })
    const user = userEvent.setup()

    render(<MfaForm />)
    const field = await screen.findByLabelText(/código de 6 dígitos/i)
    await user.type(field, '12ab34')

    expect(field).toHaveValue('1234')
  })
})
