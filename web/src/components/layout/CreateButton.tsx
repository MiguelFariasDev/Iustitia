'use client'

import { Plus } from 'lucide-react'
import { useRouter } from 'next/navigation'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { notify } from '@/lib/notify'
import { createActions, type CreateAction } from './createActions'

export function useRunCreateAction() {
  const router = useRouter()
  return (action: CreateAction) => {
    if (action.href) {
      router.push(action.href)
      return
    }
    notify.info(`${action.label}: disponível em breve`, {
      description: 'Este módulo será liberado nas próximas fases.',
    })
  }
}

export function CreateButton() {
  const run = useRunCreateAction()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button size="sm" className="gap-1.5" aria-label="Criar novo item">
          <Plus aria-hidden />
          <span className="hidden sm:inline">Criar</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        <DropdownMenuLabel>Criar</DropdownMenuLabel>
        {createActions
          .filter((a) => !a.href)
          .map((action) => (
            <DropdownMenuItem key={action.id} onSelect={() => run(action)}>
              <action.icon aria-hidden />
              {action.label}
            </DropdownMenuItem>
          ))}
        <DropdownMenuSeparator />
        {createActions
          .filter((a) => a.href)
          .map((action) => (
            <DropdownMenuItem key={action.id} onSelect={() => run(action)}>
              <action.icon aria-hidden />
              {action.label}
            </DropdownMenuItem>
          ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
