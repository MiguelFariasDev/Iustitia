'use client'

import { forwardRef, useCallback } from 'react'
import { Input } from '@/components/ui/input'

interface CurrencyInputProps extends Omit<
  React.ComponentProps<'input'>,
  'onChange' | 'value' | 'type'
> {
  /** Valor em reais, ou `null` quando o campo está vazio. */
  value: number | null
  onValueChange: (value: number | null) => void
}

const formatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
})

/** Texto digitado → reais. Só os dígitos importam: os dois últimos são os centavos. */
export function parseCurrency(text: string): number | null {
  const digits = text.replace(/\D/g, '')
  if (digits === '') return null

  return Number(digits) / 100
}

export function formatCurrencyInput(value: number | null): string {
  // Espaço inseparável do Intl vira espaço comum, para o valor colar/ler sem surpresa.
  return value === null ? '' : formatter.format(value).replace(/ /g, ' ')
}

/**
 * Campo de valor em reais com máscara "R$ 1.234,56".
 *
 * Funciona como uma calculadora: cada dígito entra pela direita, sem o usuário digitar
 * vírgula. É a máscara que mantém o cursor no lugar — recalcular a partir do texto inteiro a
 * cada tecla, com vírgula manual, faria o cursor saltar. O valor do formulário é um
 * `number`, nunca a string mascarada, para o Zod validar `> 0` sem parsing.
 */
export const CurrencyInput = forwardRef<HTMLInputElement, CurrencyInputProps>(
  function CurrencyInput({ value, onValueChange, ...props }, ref) {
    const handleChange = useCallback(
      (event: React.ChangeEvent<HTMLInputElement>) =>
        onValueChange(parseCurrency(event.target.value)),
      [onValueChange]
    )

    return (
      <Input
        ref={ref}
        inputMode="numeric"
        autoComplete="off"
        placeholder="R$ 0,00"
        value={formatCurrencyInput(value)}
        onChange={handleChange}
        {...props}
      />
    )
  }
)
