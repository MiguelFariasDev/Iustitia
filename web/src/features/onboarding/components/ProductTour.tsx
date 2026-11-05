'use client'

import { useSearchParams } from 'next/navigation'
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/authStore'
import { isFinished } from '../model'
import { useOnboardingStore } from '../store'

interface TourStop {
  /** Localiza o elemento REAL da tela (sem depender de classes de estilo). */
  find: () => HTMLElement | null
  title: string
  text: string
}

/**
 * Âncoras reais do painel. {{CONFIRMAR}}: o prompt cita também o "botão da Athena", que ainda não
 * existe no app (a Athena é a Fase 5) — por isso são 3 paradas, não 4.
 */
const stops: TourStop[] = [
  {
    find: () => document.querySelector<HTMLElement>('section[aria-labelledby="metrics-heading"]'),
    title: 'Para revisar',
    text: 'Aqui você vê, de relance, o que espera a sua decisão. “Aguardando revisão humana” é o número mais importante do dia.',
  },
  {
    find: () => document.getElementById('publications-heading')?.closest<HTMLElement>('section, div[class*="card"], [data-slot="card"]') ?? null,
    title: 'Fila de trabalho',
    text: 'As publicações chegam aqui. Abra uma para ver a sugestão da Athena e aceitar, corrigir ou rejeitar.',
  },
  {
    find: () => document.querySelector<HTMLElement>('[data-tour="search"]'),
    title: 'Busca rápida',
    text: 'Pressione Ctrl K (⌘ K no Mac) para achar processos, clientes e publicações de qualquer tela.',
  },
]

const seenKey = (userId: string) => `iu-tour-seen:${userId}`
const PAD = 6

/** Tour guiado do Painel: destaque violeta, fundo escurecido, foco gerenciado, Próximo/Pular. */
export function ProductTour() {
  const user = useAuthStore((s) => s.user)
  const state = useOnboardingStore((s) => s.state)
  const requested = useSearchParams().get('tour') === '1'
  const [index, setIndex] = useState<number | null>(null)
  const [rect, setRect] = useState<DOMRect | null>(null)
  const nextRef = useRef<HTMLButtonElement>(null)
  const opener = useRef<Element | null>(null)

  // Abre uma vez após concluir o onboarding do escritório, ou quando pedido (?tour=1).
  useEffect(() => {
    if (index !== null || !user) return
    let seen = false
    try {
      seen = Boolean(localStorage.getItem(seenKey(user.id)))
    } catch {
      seen = true
    }
    const auto = state?.flow === 'office' && isFinished(state) && !seen
    if (!requested && !auto) return
    // Assíncrono de propósito: abre depois que o painel pintou e evita setState síncrono em effect.
    const timer = setTimeout(() => {
      opener.current = document.activeElement
      setIndex(0)
    }, 0)
    return () => clearTimeout(timer)
  }, [user, state, requested, index])

  const close = useCallback(() => {
    if (user) {
      try {
        localStorage.setItem(seenKey(user.id), '1')
      } catch {
        // ignorado
      }
    }
    setIndex(null)
    setRect(null)
    ;(opener.current as HTMLElement | null)?.focus?.()
  }, [user])

  const measure = useCallback(() => {
    if (index === null) return
    const target = stops[index]?.find()
    if (!target) return setRect(null)
    target.scrollIntoView({ block: 'center', behavior: 'auto' })
    setRect(target.getBoundingClientRect())
  }, [index])

  useLayoutEffect(() => {
    const frame = requestAnimationFrame(measure)
    window.addEventListener('resize', measure)
    window.addEventListener('scroll', measure, true)
    return () => {
      cancelAnimationFrame(frame)
      window.removeEventListener('resize', measure)
      window.removeEventListener('scroll', measure, true)
    }
  }, [measure])

  // Foco gerenciado + Esc para sair.
  useEffect(() => {
    if (index === null) return
    nextRef.current?.focus()
    const onKey = (event: KeyboardEvent) => event.key === 'Escape' && close()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [index, close])

  // Pula paradas cujo elemento não existe (ex.: a lista ainda carregando ou perfil sem acesso).
  useEffect(() => {
    if (index === null || rect) return
    const timer = setTimeout(() => {
      if (stops[index]?.find()) return measure()
      if (index + 1 < stops.length) setIndex(index + 1)
      else close()
    }, 400)
    return () => clearTimeout(timer)
  }, [index, rect, measure, close])

  if (index === null || !rect) return null
  const stop = stops[index]!
  const last = index === stops.length - 1
  const below = rect.bottom + 180 < window.innerHeight
  const top = below ? rect.bottom + PAD + 10 : Math.max(8, rect.top - 170)
  const left = Math.min(Math.max(8, rect.left), Math.max(8, window.innerWidth - 336))

  return (
    <div className="iu">
      <div
        aria-hidden
        className="pointer-events-none fixed z-[60] rounded-lg"
        style={{
          top: rect.top - PAD,
          left: rect.left - PAD,
          width: rect.width + PAD * 2,
          height: rect.height + PAD * 2,
          boxShadow: '0 0 0 9999px rgba(15, 23, 42, 0.4)',
          outline: '2px solid var(--iu-accent)',
        }}
      />
      <div
        role="dialog"
        aria-label={`Tour: ${stop.title}`}
        className="iu-in fixed z-[61] w-80 max-w-[calc(100vw-1rem)] rounded-[14px] border bg-card p-4 shadow-lg"
        style={{ top, left }}
      >
        <p className="iu-mono text-xs text-muted-foreground">{index + 1} / {stops.length}</p>
        <p className="mt-1 font-semibold">{stop.title}</p>
        <p className="mt-1 text-sm text-muted-foreground">{stop.text}</p>
        <div className="mt-4 flex justify-end gap-2">
          <Button type="button" variant="ghost" className="h-10" onClick={close}>
            Pular
          </Button>
          <Button ref={nextRef} type="button" className="h-10" onClick={() => (last ? close() : setIndex(index + 1))}>
            {last ? 'Concluir' : 'Próximo'}
          </Button>
        </div>
      </div>
    </div>
  )
}
