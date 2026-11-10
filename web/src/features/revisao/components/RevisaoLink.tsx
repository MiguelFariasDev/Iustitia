'use client'

import { Sparkles } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { env } from '@/config/env'
import { routes } from '@/config/routes'
import { useAccess } from '@/hooks/useAccess'

/**
 * Atalho para a revisão da IA, na tela de publicações. Só aparece para quem pode revisar e só em
 * demonstração: sem mocks a tela de revisão não tem sugestões a mostrar (ver RevisaoView).
 */
export function RevisaoLink() {
  const { can } = useAccess()
  if (!env.useMocks || !can('review')) return null

  return (
    <Button variant="outline" size="sm" asChild>
      <Link href={`${routes.publications}/revisao` as never}>
        <Sparkles aria-hidden />
        Revisar com IA
      </Link>
    </Button>
  )
}
