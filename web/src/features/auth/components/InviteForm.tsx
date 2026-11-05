'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery } from '@tanstack/react-query'
import { AlertCircle, Building2, MailX } from 'lucide-react'
import type { Route } from 'next'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useForm, useWatch } from 'react-hook-form'
import { EmptyState } from '@/components/shared/EmptyState'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { routes } from '@/config/routes'
import { getErrorMessage } from '@/lib/api/errors'
import { userRoleLabels } from '@/lib/auth/types'
import { formatDate } from '@/lib/format'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { acceptInvite, getInvite } from '../api'
import { acceptInviteSchema, type AcceptInviteValues } from '../schemas'
import { AuthCard } from './AuthCard'
import { PasswordInput } from './PasswordInput'
import { PasswordRequirements } from './PasswordRequirements'

export function InviteForm({ token }: { token: string }) {
  const router = useRouter()
  const invite = useQuery({
    queryKey: queryKeys.invites.byToken(token),
    queryFn: () => getInvite(token),
    retry: false,
  })

  const accept = useMutation({
    mutationFn: acceptInvite,
    onSuccess: () => {
      notify.success('Convite aceito! Entre com sua nova senha.')
      router.replace(`${routes.login}?next=${encodeURIComponent(routes.onboarding)}` as Route)
    },
  })

  const form = useForm<AcceptInviteValues>({
    resolver: zodResolver(acceptInviteSchema),
    defaultValues: { name: '', password: '', confirmPassword: '', acceptTerms: false },
  })
  const password = useWatch({ control: form.control, name: 'password' })

  if (invite.isPending) {
    return (
      <AuthCard title="Carregando convite…">
        <div className="space-y-3" aria-hidden>
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-9 w-full" />
          <Skeleton className="h-9 w-full" />
        </div>
        <LoadingSpinner label="Carregando convite…" />
      </AuthCard>
    )
  }

  if (invite.isError) {
    return (
      <AuthCard title="Convite indisponível">
        <EmptyState
          icon={MailX}
          title="Não foi possível usar este convite"
          description={getErrorMessage(invite.error)}
          action={
            <Button asChild variant="outline">
              <Link href={routes.login}>Ir para o login</Link>
            </Button>
          }
        />
      </AuthCard>
    )
  }

  const details = invite.data

  return (
    <AuthCard title="Aceitar convite" description="Crie sua senha para acessar o escritório.">
      <div className="flex gap-3 rounded-xl border bg-card p-4">
        <Building2 className="mt-0.5 size-5 shrink-0 text-secondary" aria-hidden />
        <div className="space-y-0.5 text-sm">
          <p>
            <strong>{details.invitedBy}</strong> convidou você para{' '}
            <strong>{details.tenantName}</strong> como{' '}
            <strong>{userRoleLabels[details.role]}</strong>.
          </p>
          <p className="text-muted-foreground">
            {details.email} · válido até {formatDate(details.expiresAt)}
          </p>
        </div>
      </div>

      {accept.isError ? (
        <Alert variant="destructive" role="alert">
          <AlertCircle aria-hidden />
          <AlertDescription>{getErrorMessage(accept.error)}</AlertDescription>
        </Alert>
      ) : null}

      <Form {...form}>
        <form
          onSubmit={form.handleSubmit((v) =>
            accept.mutate({ token, name: v.name, password: v.password })
          )}
          className="space-y-4"
          noValidate
        >
          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Nome completo</FormLabel>
                <FormControl>
                  <Input autoComplete="name" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="password"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Senha</FormLabel>
                <FormControl>
                  <PasswordInput autoComplete="new-password" {...field} />
                </FormControl>
                <PasswordRequirements value={password} />
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="confirmPassword"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Confirmar senha</FormLabel>
                <FormControl>
                  <PasswordInput autoComplete="new-password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <FormField
            control={form.control}
            name="acceptTerms"
            render={({ field }) => (
              <FormItem>
                <div className="flex items-start gap-2">
                  <FormControl>
                    <Checkbox
                      checked={field.value}
                      onCheckedChange={(c) => field.onChange(c === true)}
                    />
                  </FormControl>
                  <FormLabel className="leading-snug font-normal">
                    Li e aceito os termos de uso e a política de privacidade (LGPD).
                  </FormLabel>
                </div>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button
            type="submit"
            className="w-full"
            disabled={accept.isPending}
            aria-busy={accept.isPending}
          >
            {accept.isPending ? <LoadingSpinner size="sm" decorative /> : null}
            Aceitar convite
          </Button>
        </form>
      </Form>
    </AuthCard>
  )
}
