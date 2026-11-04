'use client'

import dynamic from 'next/dynamic'
import { useState } from 'react'
import { useUiStore } from '@/stores/uiStore'

const CommandPalette = dynamic(() => import('./CommandPalette'), { ssr: false })

/**
 * Só baixa o chunk da paleta (cmdk + busca) na primeira abertura, e o mantém montado
 * depois disso para preservar animações e o retorno de foco do diálogo.
 */
export function CommandPaletteLoader() {
  const open = useUiStore((s) => s.commandPaletteOpen)
  const [hasOpened, setHasOpened] = useState(false)
  if (open && !hasOpened) setHasOpened(true)

  return hasOpened ? <CommandPalette /> : null
}
