import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from '@/test/render'
import { ContatoForm } from './ContatoForm'

function renderForm(onSubmit = vi.fn()) {
  const utils = renderWithProviders(
    <ContatoForm
      submitLabel="Salvar"
      isSubmitting={false}
      onSubmit={onSubmit}
      cancelHref="/contatos"
    />
  )
  return { ...utils, onSubmit }
}

describe('ContatoForm', () => {
  it('mostra os campos e o link de cancelar', () => {
    renderForm()

    expect(screen.getByLabelText('Nome')).toBeInTheDocument()
    expect(screen.getByLabelText('CPF ou CNPJ')).toBeInTheDocument()
    expect(screen.getByLabelText('E-mail')).toBeInTheDocument()
    expect(screen.getByLabelText('Telefone')).toBeInTheDocument()
    expect(screen.getByLabelText('Observações')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Cancelar' })).toHaveAttribute('href', '/contatos')
  })

  it('não envia sem nome e mostra o erro no campo', async () => {
    const { user, onSubmit } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Informe o nome do contato.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('aplica a máscara de CPF enquanto digita e envia os valores', async () => {
    const { user, onSubmit } = renderForm()

    await user.type(screen.getByLabelText('Nome'), 'Carlos Pereira')
    await user.type(screen.getByLabelText('CPF ou CNPJ'), '52998224725')
    expect(screen.getByLabelText('CPF ou CNPJ')).toHaveValue('529.982.247-25')

    await user.click(screen.getByRole('button', { name: 'Salvar' }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))
    expect(onSubmit.mock.calls[0]![0]).toMatchObject({
      nome: 'Carlos Pereira',
      tipo: 'ParteContraria',
      documento: '529.982.247-25',
    })
  })

  it('recusa CPF com dígito verificador errado', async () => {
    const { user, onSubmit } = renderForm()

    await user.type(screen.getByLabelText('Nome'), 'Carlos')
    await user.type(screen.getByLabelText('CPF ou CNPJ'), '52998224726')
    await user.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText(/Documento inválido/)).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('desabilita o envio enquanto salva', () => {
    renderWithProviders(
      <ContatoForm submitLabel="Salvar" isSubmitting onSubmit={vi.fn()} cancelHref="/contatos" />
    )

    expect(screen.getByRole('button', { name: /Salvar/ })).toBeDisabled()
  })
})
