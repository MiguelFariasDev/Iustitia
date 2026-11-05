'use client'

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { Button } from '@/components/ui/button'
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { inviteUser, listUsers } from '@/features/settings/api'
import { inviteUserSchema, type InviteUserValues } from '@/features/settings/schemas'
import { getErrorMessage } from '@/lib/api/errors'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { inviteRoles } from '../../catalog'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

/** Passo 4: convidar a equipe. Convites expiram em 7 dias; o status vem do servidor. */
export function StepTeam({ state, onComplete, onPatch, onSkip, onBack }: StepProps) {
  const queryClient = useQueryClient()
  const sent = state.data.invites ?? []
  const users = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers, staleTime: 15_000 })

  const form = useForm<InviteUserValues>({
    resolver: zodResolver(inviteUserSchema),
    defaultValues: { email: '', role: undefined },
  })
  const role = useWatch({ control: form.control, name: 'role' })
  const summary = inviteRoles.find((r) => r.value === role)?.summary

  const invite = useMutation({
    mutationFn: inviteUser,
    onSuccess: (_, values) => {
      // Idempotente: reenviar para o mesmo e-mail não duplica o registro do passo.
      const email = values.email.trim().toLowerCase()
      if (!sent.some((s) => s.email === email)) onPatch({ invites: [...sent, { email, role: values.role }] })
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.list() })
      notify.success('Convite enviado', { description: `${email} tem 7 dias para aceitar.` })
      form.reset({ email: '', role: values.role })
    },
    onError: (err) => form.setError('email', { message: getErrorMessage(err) }),
  })

  const statusOf = (email: string) =>
    users.data?.find((u) => u.email.toLowerCase() === email)?.status === 'active' ? 'Aceito' : 'Pendente'

  return (
    <div className="space-y-6">
      <Form {...form}>
        <form onSubmit={form.handleSubmit((values) => invite.mutate(values))} className="space-y-3" noValidate>
          <div className="grid gap-3 sm:grid-cols-[1fr_11rem_auto] sm:items-start">
            <FormField
              control={form.control}
              name="email"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>E-mail</FormLabel>
                  <FormControl>
                    <Input className="h-11" type="email" autoComplete="off" inputMode="email" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="role"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Perfil</FormLabel>
                  <Select onValueChange={field.onChange} value={field.value ?? ''}>
                    <FormControl>
                      <SelectTrigger className="h-11 w-full">
                        <SelectValue placeholder="Selecione" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {inviteRoles.map((r) => (
                        <SelectItem key={r.value} value={r.value}>
                          {r.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
            <Button type="submit" variant="outline" className="mt-[1.65rem] h-11" disabled={invite.isPending}>
              Enviar convite
            </Button>
          </div>
          {summary ? (
            <p className="iu-in text-sm text-muted-foreground" aria-live="polite">
              <strong className="text-foreground">O que este perfil acessa:</strong> {summary}
            </p>
          ) : null}
        </form>
      </Form>

      {sent.length > 0 ? (
        <section aria-labelledby="convites" className="space-y-2">
          <h3 id="convites" className="text-sm font-semibold">
            Convites enviados
          </h3>
          <ul className="divide-y rounded-[14px] border bg-card">
            {sent.map((item) => {
              const status = statusOf(item.email)
              return (
                <li key={item.email} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                  <span className="min-w-0 flex-1 break-all">{item.email}</span>
                  <span className="text-muted-foreground">{inviteRoles.find((r) => r.value === item.role)?.label ?? item.role}</span>
                  <span className={status === 'Aceito' ? 'text-[var(--iu-ok)]' : 'text-[var(--iu-warn)]'}>{status}</span>
                  {status === 'Pendente' ? (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="min-h-9"
                      disabled={invite.isPending}
                      onClick={() => invite.mutate({ email: item.email, role: item.role as InviteUserValues['role'] })}
                    >
                      Reenviar
                    </Button>
                  ) : null}
                </li>
              )
            })}
          </ul>
        </section>
      ) : null}

      <StepActions
        onBack={onBack}
        onSkip={onSkip}
        onContinue={() => onComplete()}
        continueLabel={sent.length > 0 ? 'Continuar' : 'Continuar sem convidar'}
      />
    </div>
  )
}
