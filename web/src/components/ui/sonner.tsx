'use client'

import {
  CircleCheckIcon,
  InfoIcon,
  Loader2Icon,
  OctagonXIcon,
  TriangleAlertIcon,
} from 'lucide-react'
import { Toaster as Sonner, type ToasterProps } from 'sonner'
import { useTheme } from '@/hooks/useTheme'

const Toaster = ({ ...props }: ToasterProps) => {
  const { resolvedTheme } = useTheme()

  return (
    <Sonner
      theme={resolvedTheme}
      // Centralizado embaixo (34px do fundo), como no protótipo. O `offset` empurra o contêiner;
      // o centramento é do próprio Sonner (sem translateX, que a animação de entrada anularia).
      position="bottom-center"
      offset={34}
      className="toaster group"
      containerAriaLabel="Notificações"
      icons={{
        success: <CircleCheckIcon className="size-4" />,
        info: <InfoIcon className="size-4" />,
        warning: <TriangleAlertIcon className="size-4" />,
        error: <OctagonXIcon className="size-4" />,
        loading: <Loader2Icon className="size-4 animate-spin" />,
      }}
      style={
        {
          // Toast escuro fixo nos dois temas (token --toast), legível sobre qualquer tela.
          '--normal-bg': 'var(--toast)',
          '--normal-text': 'var(--toast-foreground)',
          '--normal-border': 'transparent',
          '--border-radius': 'var(--radius)',
        } as React.CSSProperties
      }
      {...props}
    />
  )
}

export { Toaster }
