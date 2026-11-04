'use client'

import { CheckCheck } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useAlertaSummary, useMarkAllAlertasAsRead } from '../hooks'

export function MarcarTodosButton() {
  const { data } = useAlertaSummary()
  const markAll = useMarkAllAlertasAsRead()

  return (
    <Button
      variant="outline"
      size="sm"
      disabled={markAll.isPending || (data?.unread ?? 0) === 0}
      onClick={() => markAll.mutate()}
    >
      <CheckCheck aria-hidden />
      Marcar todos como lidos
    </Button>
  )
}
