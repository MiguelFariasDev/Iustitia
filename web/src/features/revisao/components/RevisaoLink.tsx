'use client'

import { Sparkles } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { useAccess } from '@/hooks/useAccess'

/**
 * Atalho para a revisão da IA, na tela de publicações. Só aparece para quem pode revisar.
 */
export function RevisaoLink() {
  const { can } = useAccess()
  if (!can('review')) return null

  return (
    <Button variant="outline" size="sm" asChild>
      <Link href={`${routes.publications}/revisao` as never}>
        <Sparkles aria-hidden />
        Revisar com IA
      </Link>
    </Button>
  )
}
