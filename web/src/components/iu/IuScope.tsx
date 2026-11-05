'use client'

import { useEffect } from 'react'
import { cn } from '@/lib/utils'

interface IuScopeProps {
  /** Classe da variável da fonte (geist.variable), vinda do layout (server). */
  fontClass: string
  className?: string
  children: React.ReactNode
}

/**
 * Raiz das superfícies v4. Além do wrapper `.iu`, marca o <body> enquanto está montada: menus,
 * seletores e gavetas (Radix) renderizam em portal, FORA do wrapper, e sem isto voltariam aos
 * tokens do app autenticado.
 */
export function IuScope({ fontClass, className, children }: IuScopeProps) {
  useEffect(() => {
    const classes = ['iu', ...fontClass.split(' ')].filter(Boolean)
    document.body.classList.add(...classes)
    return () => document.body.classList.remove(...classes)
  }, [fontClass])

  return <div className={cn('iu min-h-dvh', fontClass, className)}>{children}</div>
}
