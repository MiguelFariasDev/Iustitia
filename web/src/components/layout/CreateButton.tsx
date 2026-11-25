'use client'

import { Plus } from 'lucide-react'
import { usePathname, useRouter } from 'next/navigation'
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
import { createActions, hrefNoProcesso, opcoesNoProcesso, processoDaRota, type CreateAction } from './createActions'

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
  const router = useRouter()
  const processoId = processoDaRota(usePathname())

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button className="h-10 gap-1.5 px-4" aria-label="Criar novo item">
          <Plus aria-hidden />
          <span className="hidden sm:inline">Novo</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-52">
        {processoId ? (
          <>
            <DropdownMenuLabel>Neste processo</DropdownMenuLabel>
            {opcoesNoProcesso.map((opcao) => (
              <DropdownMenuItem
                key={opcao.id}
                onSelect={() => router.push(hrefNoProcesso(processoId, opcao.id))}
              >
                <Plus aria-hidden />
                {opcao.label}
              </DropdownMenuItem>
            ))}
            <DropdownMenuSeparator />
          </>
        ) : null}
        <DropdownMenuLabel>Novo</DropdownMenuLabel>
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
