'use client'

import { useEffect, useRef } from 'react'

interface RevealProps extends React.HTMLAttributes<HTMLElement> {
  as?: 'div' | 'section' | 'li' | 'article'
  /** Posição na grade: atraso escalonado de 60 ms por índice. */
  index?: number
}

/**
 * Entrada ao rolar (`iu-in`), disparada UMA vez por IntersectionObserver. Só "arma" (oculta) o que
 * está abaixo da dobra e só depois da hidratação: sem JS, ou com `prefers-reduced-motion`, o
 * conteúdo nunca fica invisível (o CSS neutraliza a armação).
 */
export function Reveal({ as: Tag = 'div', index = 0, className, style, children, ...rest }: RevealProps) {
  const ref = useRef<HTMLElement>(null)

  useEffect(() => {
    const element = ref.current
    if (!element) return

    const rect = element.getBoundingClientRect()
    if (rect.top < window.innerHeight || typeof IntersectionObserver === 'undefined') return

    element.setAttribute('data-armed', '')
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          element.setAttribute('data-revealed', '')
          observer.disconnect()
        }
      },
      { rootMargin: '0px 0px -8% 0px' }
    )
    observer.observe(element)
    return () => observer.disconnect()
  }, [])

  return (
    <Tag
      ref={ref as never}
      data-reveal=""
      className={className}
      style={{ ...style, ['--i' as string]: index }}
      {...rest}
    >
      {children}
    </Tag>
  )
}

