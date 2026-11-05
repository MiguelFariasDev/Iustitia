'use client'

import Link from 'next/link'
import { usePathname, useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { routes } from '@/config/routes'
import { useAuthStore } from '@/stores/authStore'
import { isFinished, progress } from '../model'
import { useOnboardingStore } from '../store'

const REDIRECTED_KEY = 'iu-onboarding-redirected'

/**
 * Guarda de rota SUAVE do onboarding (app autenticado):
 *  - carrega o estado do usuário;
 *  - se o onboarding está incompleto, leva a /onboarding UMA vez por sessão do navegador
 *    (ninguém fica preso: dá para sair para o app a qualquer momento);
 *  - depois disso, mostra o banner discreto "Concluir configuração · 3 de 6".
 */
export function OnboardingGate() {
  const user = useAuthStore((s) => s.user)
  const { status, state, load } = useOnboardingStore()
  const router = useRouter()
  const pathname = usePathname()

  useEffect(() => {
    if (user) void load(user)
  }, [user, load])

  useEffect(() => {
    if (status !== 'ready' || !state || isFinished(state)) return
    try {
      if (sessionStorage.getItem(REDIRECTED_KEY)) return
      sessionStorage.setItem(REDIRECTED_KEY, '1')
    } catch {
      return
    }
    router.replace(routes.onboarding)
  }, [status, state, router])

  if (status !== 'ready' || !state || isFinished(state) || pathname === routes.onboarding) return null

  const { done, total } = progress(state)
  return (
    <div role="region" aria-label="Configuração inicial" className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border border-secondary/30 bg-secondary/5 px-4 py-2 text-sm">
      <span className="text-foreground">
        Concluir configuração · <strong className="tabular">{done} de {total}</strong>
      </span>
      <Link href={routes.onboarding} className="ml-auto font-medium text-secondary underline-offset-4 hover:underline">
        Continuar de onde parei
      </Link>
    </div>
  )
}
