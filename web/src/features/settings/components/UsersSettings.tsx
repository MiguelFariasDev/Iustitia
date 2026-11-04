'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { ColumnDef } from '@tanstack/react-table'
import { MoreHorizontal, UserMinus, UserPlus } from 'lucide-react'
import type { Route } from 'next'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { DataTable } from '@/components/shared/DataTable'
import { ErrorFallback } from '@/components/shared/ErrorFallback'
import { StatusBadge } from '@/components/shared/StatusBadge'
import type { Tone } from '@/components/shared/tones'
import { Avatar, AvatarFallback } from '@/components/ui/avatar'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuth } from '@/hooks/useAuth'
import { getErrorMessage } from '@/lib/api/errors'
import { userRoleLabels } from '@/lib/auth/types'
import { formatRelative, getInitials } from '@/lib/format'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { listUsers, removeUser } from '../api'
import type { TeamUser, TeamUserStatus } from '../types'
import { InviteUserDialog } from './InviteUserDialog'

const statusConfig: Record<TeamUserStatus, { label: string; tone: Tone }> = {
  active: { label: 'Ativo', tone: 'success' },
  invited: { label: 'Convite pendente', tone: 'warning' },
  suspended: { label: 'Suspenso', tone: 'neutral' },
}

export function UsersSettings() {
  const queryClient = useQueryClient()
  const { user: currentUser } = useAuth()
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const inviteOpen = searchParams.get('convidar') === '1'
  const [toRemove, setToRemove] = useState<TeamUser | null>(null)

  const users = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers })

  const remove = useMutation({
    mutationFn: (user: TeamUser) => removeUser(user.id),
    onSuccess: (_data, user) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all })
      notify.success('Acesso removido.', {
        description: `${user.name} não poderá mais entrar no escritório.`,
      })
      setToRemove(null)
    },
    onError: (error) =>
      notify.error('Não foi possível remover o acesso.', { description: getErrorMessage(error) }),
  })

  // O estado do diálogo de convite vive na URL (?convidar=1): o botão global "Criar" abre direto.
  function setInviteOpen(open: boolean) {
    const params = new URLSearchParams(searchParams.toString())
    if (open) params.set('convidar', '1')
    else params.delete('convidar')
    const query = params.toString()
    router.replace(`${pathname}${query ? `?${query}` : ''}` as Route, { scroll: false })
  }

  const columns: ColumnDef<TeamUser, unknown>[] = [
    {
      // Nome + e-mail no mesmo valor: o filtro global da tabela casa com ambos.
      id: 'name',
      accessorFn: (u) => `${u.name} ${u.email}`,
      header: 'Nome',
      cell: ({ row }) => (
        <div className="flex items-center gap-3">
          <Avatar className="size-8">
            <AvatarFallback className="text-xs">{getInitials(row.original.name)}</AvatarFallback>
          </Avatar>
          <div className="min-w-0">
            <p className="truncate font-medium">{row.original.name}</p>
            <p className="truncate text-xs text-muted-foreground">{row.original.email}</p>
          </div>
        </div>
      ),
    },
    {
      accessorKey: 'role',
      header: 'Papel',
      cell: ({ row }) => userRoleLabels[row.original.role],
    },
    {
      accessorKey: 'status',
      header: 'Situação',
      cell: ({ row }) => {
        const { label, tone } = statusConfig[row.original.status]
        return <StatusBadge tone={tone}>{label}</StatusBadge>
      },
    },
    {
      accessorKey: 'lastSignInAt',
      header: 'Último acesso',
      sortingFn: 'datetime',
      cell: ({ row }) =>
        row.original.lastSignInAt ? (
          <time dateTime={row.original.lastSignInAt}>
            {formatRelative(row.original.lastSignInAt)}
          </time>
        ) : (
          <span className="text-muted-foreground">—</span>
        ),
    },
    {
      id: 'actions',
      header: () => <span className="sr-only">Ações</span>,
      enableSorting: false,
      cell: ({ row }) => {
        const target = row.original
        const isSelf = target.id === currentUser?.id || target.role === 'Owner'
        return (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon-sm" aria-label={`Ações para ${target.name}`}>
                <MoreHorizontal aria-hidden />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem
                variant="destructive"
                disabled={isSelf}
                onSelect={() => setToRemove(target)}
              >
                <UserMinus aria-hidden />
                Remover acesso
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        )
      },
    },
  ]

  if (users.isError)
    return <ErrorFallback error={users.error} onRetry={() => void users.refetch()} />

  return (
    <>
      <DataTable
        caption="Usuários do escritório"
        columns={columns}
        data={users.data ?? []}
        isLoading={users.isPending}
        getRowId={(u) => u.id}
        searchPlaceholder="Filtrar por nome ou e-mail"
        emptyTitle="Nenhum usuário encontrado"
        toolbar={
          <Button onClick={() => setInviteOpen(true)}>
            <UserPlus aria-hidden />
            Convidar usuário
          </Button>
        }
      />
      <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} />
      <ConfirmDialog
        open={toRemove !== null}
        onOpenChange={(open) => {
          if (!open) setToRemove(null)
        }}
        destructive
        title="Remover acesso?"
        description={`${toRemove?.name ?? ''} perderá o acesso imediatamente. O histórico de ações continua na auditoria.`}
        confirmLabel="Remover acesso"
        isPending={remove.isPending}
        onConfirm={() => {
          if (toRemove) remove.mutate(toRemove)
        }}
      />
    </>
  )
}
