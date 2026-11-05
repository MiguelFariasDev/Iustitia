'use client'

import { useId, useRef } from 'react'
import { cn } from '@/lib/utils'

interface OtpInputProps {
  value: string
  onChange: (value: string) => void
  /** Chamado quando o 6º dígito é digitado/colado. */
  onComplete?: (value: string) => void
  length?: number
  disabled?: boolean
  invalid?: boolean
  autoFocus?: boolean
  label: string
  describedBy?: string
}

/**
 * Código de N dígitos em células. Por baixo há UM `<input>` real (transparente) — assim o
 * autopreenchimento do SO (`one-time-code`), colar e leitores de tela funcionam nativamente; as
 * células são só a apresentação.
 */
export function OtpInput({
  value,
  onChange,
  onComplete,
  length = 6,
  disabled,
  invalid,
  autoFocus,
  label,
  describedBy,
}: OtpInputProps) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)

  function handle(raw: string) {
    const digits = raw.replace(/\D/g, '').slice(0, length)
    onChange(digits)
    if (digits.length === length) onComplete?.(digits)
  }

  return (
    <div className="space-y-2">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <div
        className={cn(
          'relative flex gap-2 rounded-lg focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-[var(--iu-accent)]'
        )}
        onClick={() => inputRef.current?.focus()}
      >
        {Array.from({ length }, (_, index) => (
          <div
            key={index}
            aria-hidden
            className={cn(
              'iu-mono flex h-12 flex-1 items-center justify-center rounded-lg border-[1.5px] bg-card text-xl',
              invalid ? 'border-[var(--iu-danger)]' : 'border-input',
              index === Math.min(value.length, length - 1) && !disabled && 'border-[var(--iu-brand)]'
            )}
          >
            {value[index] ?? ''}
          </div>
        ))}
        <input
          ref={inputRef}
          id={id}
          value={value}
          onChange={(event) => handle(event.target.value)}
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="[0-9]*"
          maxLength={length * 2}
          disabled={disabled}
          autoFocus={autoFocus}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          aria-label={label}
          className="absolute inset-0 h-full w-full cursor-text opacity-0"
        />
      </div>
    </div>
  )
}
