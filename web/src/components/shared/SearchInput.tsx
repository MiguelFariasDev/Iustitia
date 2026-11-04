'use client'

import { Search, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

interface SearchInputProps {
  value?: string
  /** Chamado após `delayMs` sem digitação (e imediatamente ao limpar). */
  onSearch: (value: string) => void
  placeholder?: string
  label?: string
  delayMs?: number
  className?: string
}

export function SearchInput({
  value = '',
  onSearch,
  placeholder = 'Buscar…',
  label = 'Buscar',
  delayMs = 300,
  className,
}: SearchInputProps) {
  const [text, setText] = useState(value)
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  function handleChange(next: string) {
    setText(next)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => onSearch(next.trim()), delayMs)
  }

  function clear() {
    clearTimeout(timer.current)
    setText('')
    onSearch('')
  }

  return (
    <div className={cn('relative w-full sm:max-w-xs', className)}>
      <Search
        className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
        aria-hidden
      />
      <Input
        type="search"
        role="searchbox"
        aria-label={label}
        placeholder={placeholder}
        value={text}
        onChange={(e) => handleChange(e.target.value)}
        className="pr-8 pl-8 [&::-webkit-search-cancel-button]:hidden"
      />
      {text ? (
        <button
          type="button"
          onClick={clear}
          aria-label="Limpar busca"
          className="absolute top-1/2 right-1.5 flex size-6 -translate-y-1/2 items-center justify-center rounded text-muted-foreground hover:text-foreground"
        >
          <X className="size-3.5" aria-hidden />
        </button>
      ) : null}
    </div>
  )
}
