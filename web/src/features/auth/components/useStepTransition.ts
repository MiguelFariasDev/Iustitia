'use client'

import { useCallback, useEffect, useRef, useState } from 'react'

const FADE_OUT_MS = 120

/**
 * Troca de passo com saída em fade (~120 ms) e entrada `iu-in` (a entrada vem do CSS da nova
 * `key`). Com `prefers-reduced-motion` a troca é imediata. Devolve `leaving` para o contêiner
 * aplicar `opacity-0 transition-opacity`.
 */
export function useStepTransition<T>(initial: T) {
  const [step, setStep] = useState<T>(initial)
  const [leaving, setLeaving] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  const goTo = useCallback((next: T, afterChange?: () => void) => {
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    clearTimeout(timer.current)
    if (reduced) {
      setStep(next)
      requestAnimationFrame(() => afterChange?.())
      return
    }
    setLeaving(true)
    timer.current = setTimeout(() => {
      setStep(next)
      setLeaving(false)
      requestAnimationFrame(() => afterChange?.())
    }, FADE_OUT_MS)
  }, [])

  return { step, leaving, goTo }
}
