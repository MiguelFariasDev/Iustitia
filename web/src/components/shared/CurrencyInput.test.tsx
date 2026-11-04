import { screen } from '@testing-library/react'
import { useState } from 'react'
import { renderWithProviders } from '@/test/render'
import { CurrencyInput, formatCurrencyInput, parseCurrency } from './CurrencyInput'

function Harness({ onChange }: { onChange: (v: number | null) => void }) {
  const [valor, setValor] = useState<number | null>(null)
  return (
    <CurrencyInput
      aria-label="Valor"
      value={valor}
      onValueChange={(v) => {
        setValor(v)
        onChange(v)
      }}
    />
  )
}

describe('parseCurrency / formatCurrencyInput', () => {
  it('os dois últimos dígitos são os centavos', () => {
    expect(parseCurrency('12345')).toBe(123.45)
    expect(parseCurrency('R$ 1.234,56')).toBe(1234.56)
    expect(parseCurrency('5')).toBe(0.05)
  })

  it('vazio é null, não zero', () => {
    expect(parseCurrency('')).toBeNull()
    expect(parseCurrency('abc')).toBeNull()
    expect(formatCurrencyInput(null)).toBe('')
  })

  it('formata em reais com espaço comum', () => {
    expect(formatCurrencyInput(1234.5)).toBe('R$ 1.234,50')
  })
})

describe('CurrencyInput', () => {
  it('monta o valor como uma calculadora e devolve número', async () => {
    const onChange = vi.fn()
    const { user } = renderWithProviders(<Harness onChange={onChange} />)
    const campo = screen.getByLabelText('Valor')

    await user.type(campo, '12345')

    expect(campo).toHaveValue('R$ 123,45')
    expect(onChange).toHaveBeenLastCalledWith(123.45)
  })

  it('apagar tudo devolve null', async () => {
    const onChange = vi.fn()
    const { user } = renderWithProviders(<Harness onChange={onChange} />)
    const campo = screen.getByLabelText('Valor')

    await user.type(campo, '9')
    await user.clear(campo)

    expect(onChange).toHaveBeenLastCalledWith(null)
    expect(campo).toHaveValue('')
  })

  it('ignora letras', async () => {
    const { user } = renderWithProviders(<Harness onChange={vi.fn()} />)
    const campo = screen.getByLabelText('Valor')

    await user.type(campo, 'a1b0')

    expect(campo).toHaveValue('R$ 0,10')
  })
})
