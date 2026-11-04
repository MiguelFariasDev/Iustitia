'use client'

import { forwardRef, useCallback } from 'react'
import { Input } from '@/components/ui/input'
import { formatCnpj } from '@/lib/validation/cnpj'
import { formatCpf } from '@/lib/validation/documento'
import type { TipoCliente } from '../types'

interface DocumentoInputProps extends Omit<React.ComponentProps<'input'>, 'onChange' | 'value'> {
  tipo: TipoCliente
  value: string
  onChange: (value: string) => void
}

/**
 * Campo de CPF/CNPJ com máscara aplicada enquanto se digita.
 *
 * O valor guardado no formulário é o MASCARADO (o que o usuário vê); a conversão para
 * dígitos acontece no envio (ver api.ts). Guardar dígitos e mascarar só na exibição faria o
 * cursor saltar a cada tecla, porque o valor controlado mudaria de forma a cada render.
 */
export const DocumentoInput = forwardRef<HTMLInputElement, DocumentoInputProps>(
  function DocumentoInput({ tipo, value, onChange, ...props }, ref) {
    const isPessoaFisica = tipo === 'PessoaFisica'

    const handleChange = useCallback(
      (event: React.ChangeEvent<HTMLInputElement>) => {
        const raw = event.target.value
        onChange(isPessoaFisica ? formatCpf(raw) : formatCnpj(raw))
      },
      [isPessoaFisica, onChange]
    )

    return (
      <Input
        ref={ref}
        // inputMode numérico abre o teclado certo no celular sem impedir colar com máscara.
        inputMode="numeric"
        autoComplete="off"
        spellCheck={false}
        placeholder={isPessoaFisica ? '000.000.000-00' : '00.000.000/0000-00'}
        aria-label={isPessoaFisica ? 'CPF' : 'CNPJ'}
        maxLength={isPessoaFisica ? 14 : 18}
        value={value}
        onChange={handleChange}
        {...props}
      />
    )
  }
)
