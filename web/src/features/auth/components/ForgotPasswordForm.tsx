'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, MailCheck } from 'lucide-react'
import Link from 'next/link'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { routes } from '@/config/routes'
import { authService } from '@/lib/auth/authService'
import { reportError } from '@/lib/observability/reportError'
import { forgotPasswordSchema, type ForgotPasswordValues } from '../schemas'
import { AuthCard } from './AuthCard'

const backToLogin = (
  <Link
    href={routes.login}
    className="inline-flex items-center gap-1 font-medium text-secondary hover:underline"
  >
    <ArrowLeft className="size-3.5" aria-hidden />
    Voltar para o login
  </Link>
)

export function ForgotPasswordForm() {
  const [sentTo, setSentTo] = useState<string | null>(null)
  const form = useForm<ForgotPasswordValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: { email: '' },
  })

  async function onSubmit({ email }: ForgotPasswordValues) {
    try {
      await authService.resetPassword(email)
    } catch (error) {
      // Resposta sempre genérica: não revelamos se o e-mail existe (anti-enumeração).
      reportError(error)
    }
    setSentTo(email)
  }

  if (sentTo) {
    return (
      <AuthCard title="Verifique seu e-mail" footer={backToLogin}>
        <div role="status" className="flex gap-3 rounded-xl border bg-card p-4">
          <MailCheck className="mt-0.5 size-5 shrink-0 text-success" aria-hidden />
          <p className="text-sm">
            Se existir uma conta para <strong className="break-all">{sentTo}</strong>, você receberá
            um link para redefinir a senha em alguns minutos.
          </p>
        </div>
      </AuthCard>
    )
  }

  return (
    <AuthCard
      title="Esqueci minha senha"
      description="Informe o e-mail cadastrado e enviaremos um link para criar uma nova senha."
      footer={backToLogin}
    >
      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>E-mail</FormLabel>
                <FormControl>
                  <Input type="email" autoComplete="email" autoFocus {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button
            type="submit"
            className="w-full"
            disabled={form.formState.isSubmitting}
            aria-busy={form.formState.isSubmitting}
          >
            {form.formState.isSubmitting ? <LoadingSpinner size="sm" decorative /> : null}
            Enviar link
          </Button>
        </form>
      </Form>
    </AuthCard>
  )
}
