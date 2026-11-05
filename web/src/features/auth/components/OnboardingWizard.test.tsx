import { screen } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { OnboardingWizard } from './OnboardingWizard'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn(), refresh: vi.fn(), push: vi.fn() }),
}))

describe('OnboardingWizard — tipo de conta', () => {
  it('começa como escritório, com nome e CNPJ', () => {
    renderWithProviders(<OnboardingWizard />)

    expect(screen.getByRole('heading', { name: /Etapa 1 de 3: Escritório/ })).toBeInTheDocument()
    expect(screen.getByLabelText('Nome do escritório')).toBeInTheDocument()
    expect(screen.getByLabelText('CNPJ (opcional)')).toBeInTheDocument()
    expect(screen.queryByLabelText('CPF')).toBeNull()
  })

  it('oferece as duas opções de conta', () => {
    renderWithProviders(<OnboardingWizard />)

    const grupo = screen.getByRole('radiogroup', { name: 'Como você atua?' })
    expect(grupo).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: /Escritório/ })).toBeChecked()
    expect(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ })).not.toBeChecked()
  })

  it('escolher autônomo troca nome e CNPJ por CPF e renomeia a etapa', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))

    expect(screen.queryByLabelText('Nome do escritório')).toBeNull()
    expect(screen.queryByLabelText('CNPJ (opcional)')).toBeNull()
    expect(screen.getByLabelText('CPF')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: /Etapa 1 de 3: Dados profissionais/ })
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Crie sua conta' })).toBeInTheDocument()
  })

  it('autônomo não avança sem CPF', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))
    await user.click(screen.getByRole('button', { name: 'Continuar' }))

    expect(await screen.findByText('Informe o seu CPF.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Etapa 1 de 3/ })).toBeInTheDocument()
  })

  it('máscara o CPF enquanto digita', async () => {
    const { user } = renderWithProviders(<OnboardingWizard />)

    await user.click(screen.getByRole('radio', { name: /Advogado\(a\) autônomo\(a\)/ }))
    await user.type(screen.getByLabelText('CPF'), '52998224725')

    expect(screen.getByLabelText('CPF')).toHaveValue('529.982.247-25')
  })
})
