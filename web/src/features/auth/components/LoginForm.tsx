'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Info } from 'lucide-react'
import type { Route } from 'next'
import Link from 'next/link'
import { useRouter, useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Alert, AlertDescription } from '@/components/ui/alert'
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
import { isMockAuth } from '@/config/env'
import { routes, safeRedirectPath } from '@/config/routes'
import { useAuth } from '@/hooks/useAuth'
import { MOCK_PASSWORD } from '@/lib/auth/mockAuth'
import { getErrorMessage } from '@/lib/api/errors'
import { loginSchema, type LoginValues } from '../schemas'
import { AuthCard } from './AuthCard'
import { PasswordInput } from './PasswordInput'

export function LoginForm() {
  const { signIn } = useAuth()
  const router = useRouter()
  const searchParams = useSearchParams()
  const [error, setError] = useState<string | null>(null)

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setError(null)
    try {
      await signIn(values)
      router.replace(safeRedirectPath(searchParams.get('next')) as Route)
      router.refresh()
    } catch (err) {
      setError(getErrorMessage(err))
      form.setFocus('password')
    }
  }

  const { isSubmitting } = form.formState

  return (
    <AuthCard
      title="Entrar"
      description="Acesse o painel do seu escritório."
      footer={
        <>
          Ainda não usa?{' '}
          <Link
            href={routes.onboarding}
            className="font-medium text-secondary underline-offset-4 hover:underline"
          >
            Cadastre seu escritório
          </Link>
        </>
      }
    >
      {isMockAuth ? (
        // Dica estática: `role="note"` em vez do `role="alert"` padrão do Alert —
        // não deve ser anunciada assertivamente no carregamento da página.
        <Alert role="note">
          <Info aria-hidden />
          <AlertDescription>
            Modo demonstração: use qualquer e-mail e a senha{' '}
            <code className="font-mono">{MOCK_PASSWORD}</code>.
          </AlertDescription>
        </Alert>
      ) : null}

      {error ? (
        <Alert variant="destructive" role="alert">
          <AlertCircle aria-hidden />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="email"
            render={({ field }) => (
              <FormItem>
                <FormLabel>E-mail</FormLabel>
                <FormControl>
                  <Input type="email" autoComplete="email" inputMode="email" autoFocus {...field} />
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
                <div className="flex items-center justify-between">
                  <FormLabel>Senha</FormLabel>
                  <Link
                    href={routes.forgotPassword}
                    className="text-sm text-secondary underline-offset-4 hover:underline"
                  >
                    Esqueci minha senha
                  </Link>
                </div>
                <FormControl>
                  <PasswordInput autoComplete="current-password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          <Button type="submit" className="w-full" disabled={isSubmitting} aria-busy={isSubmitting}>
            {isSubmitting ? <LoadingSpinner size="sm" decorative /> : null}
            Entrar
          </Button>
        </form>
      </Form>
    </AuthCard>
  )
}
