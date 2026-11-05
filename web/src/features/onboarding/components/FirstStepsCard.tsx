'use client'

import { Check, Circle, X } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuthStore } from '@/stores/authStore'
import { checklist, isFinished } from '../model'
import { useOnboardingStore } from '../store'

const dismissKey = (userId: string) => `iu-first-steps-dismissed:${userId}`

/** Card dispensável no Painel: primeiros passos, cada um com ✓ quando feito. */
export function FirstStepsCard() {
  const user = useAuthStore((s) => s.user)
  const state = useOnboardingStore((s) => s.state)
  const [hidden, setHidden] = useState(() => {
    try {
      return Boolean(user && localStorage.getItem(dismissKey(user.id)))
    } catch {
      return false
    }
  })

  if (!user || !state || state.flow !== 'office' || !isFinished(state) || hidden) return null
  const items = checklist(state)
  const pending = items.filter((item) => !item.done && !item.soon)
  if (pending.length === 0) return null

  function dismiss() {
    try {
      localStorage.setItem(dismissKey(user!.id), '1')
    } catch {
      // sem armazenamento: some só nesta visita
    }
    setHidden(true)
  }

  return (
    <Card aria-labelledby="first-steps-title">
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle id="first-steps-title" className="text-base">
          Primeiros passos
        </CardTitle>
        <Button type="button" variant="ghost" size="icon" onClick={dismiss} aria-label="Dispensar primeiros passos">
          <X aria-hidden />
        </Button>
      </CardHeader>
      <CardContent>
        <ul className="space-y-2">
          {items.map((item) => (
            <li key={item.id} className="flex items-center gap-3 text-sm">
              {item.done ? (
                <Check className="size-4 text-success-foreground" aria-label="Feito" />
              ) : (
                <Circle className="size-4 text-muted-foreground" aria-label="Pendente" />
              )}
              {item.done || item.soon ? (
                <span className={item.done ? 'text-muted-foreground line-through' : 'text-muted-foreground'}>
                  {item.label}
                  {item.soon ? ' (em breve)' : ''}
                </span>
              ) : (
                <Link href={item.href as never} className="underline-offset-4 hover:underline">
                  {item.label}
                </Link>
              )}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}
