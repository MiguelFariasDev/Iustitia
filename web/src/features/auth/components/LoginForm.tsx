'use client'

import { zodResolver } from '@hookform/resolvers/zod'
import { AlertCircle, Info } from 'lucide-react'
import type { Route } from 'next'
import Link from 'next/link'
import { useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
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
import { OtpInput } from '@/components/iu/OtpInput'
import { isMockAuth } from '@/config/env'
import { routes, safeRedirectPath } from '@/config/routes'
import { useAuth } from '@/hooks/useAuth'
import { MOCK_PASSWORD } from '@/lib/auth/mockAuth'
import { authService } from '@/lib/auth/authService'
import {
  clearLoginFailures,
  recordLoginFailure,
  remainingLockSeconds,
} from '@/lib/auth/loginBackoff'
import { AuthError } from '@/lib/auth/types'
import { loginSchema, type LoginValues } from '../schemas'
import { AuthCard } from './AuthCard'
import { PasswordInput } from './PasswordInput'

/** Códigos em que é seguro/útil dizer algo mais específico que o erro genérico de credenciais. */
const SPECIFIC_ERROR_CODES = new Set([
  'email_not_confirmed',
  'over_request_rate_limit',
  'over_email_send_rate_limit',
])

function loginErrorMessage(err: unknown): string {
  // Sem diferenciar e-mail inexistente de senha errada (evita enumeração de contas).
  if (err instanceof AuthError && SPECIFIC_ERROR_CODES.has(err.code)) return err.message
  return 'E-mail ou senha incorretos.'
}

export function LoginForm() {
  const { signIn } = useAuth()
  const router = useRouter()
  const searchParams = useSearchParams()
  const [error, setError] = useState<string | null>(null)
  const [mfaFactor, setMfaFactor] = useState<string | null>(null)
  const [lockSeconds, setLockSeconds] = useState(0)

  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  // Retoma um bloqueio em andamento (ex.: recarregou a página) e faz a contagem regressiva.
  // Um único timer: lê o estado persistido na montagem (0 ms) e depois a cada segundo enquanto bloqueado.
  useEffect(() => {
    const timer = setTimeout(() => setLockSeconds(remainingLockSeconds()), lockSeconds > 0 ? 1000 : 0)
    return () => clearTimeout(timer)
  }, [lockSeconds])

  function finish() {
    clearLoginFailures()
    router.replace(safeRedirectPath(searchParams.get('next')) as Route)
    router.refresh()
  }

  async function onSubmit(values: LoginValues) {
    if (remainingLockSeconds() > 0) return
    setError(null)
    try {
      await signIn(values)
    } catch (err) {
      const wait = recordLoginFailure()
      setLockSeconds(wait)
      setError(loginErrorMessage(err))
      form.setFocus('password')
      return
    }

    const challenge = await authService.getMfaChallenge().catch(() => null)
    if (challenge) {
      setMfaFactor(challenge.factorId)
      return
    }
    finish()
  }

  if (mfaFactor) {
    return <MfaStep factorId={mfaFactor} onVerified={finish} />
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
            href={routes.signup}
            className="font-medium text-secondary underline-offset-4 hover:underline"
          >
            Crie seu escritório
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
                  <Input className="h-11" type="email" autoComplete="email" inputMode="email" autoFocus {...field} />
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
                  <PasswordInput className="h-11" autoComplete="current-password" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />
          {lockSeconds > 0 ? (
            <p role="status" className="text-sm text-[var(--iu-warn)]">
              Muitas tentativas. Por segurança, aguarde {lockSeconds}s para tentar de novo.
            </p>
          ) : null}
          <Button
            type="submit"
            className="iu-btn-lg w-full"
            disabled={isSubmitting || lockSeconds > 0}
            aria-busy={isSubmitting}
          >
            {isSubmitting ? <LoadingSpinner size="sm" decorative /> : null}
            Entrar
          </Button>
        </form>
      </Form>
    </AuthCard>
  )
}

/** Segunda etapa do login: código de 6 dígitos do aplicativo autenticador. */
function MfaStep({ factorId, onVerified }: { factorId: string; onVerified: () => void }) {
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function verify(value: string) {
    if (busy || value.length !== 6) return
    setBusy(true)
    setError(null)
    try {
      await authService.verifyMfa(factorId, value)
      onVerified()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Código inválido.')
      setCode('')
      setBusy(false)
    }
  }

  return (
    <AuthCard
      title="Verificação em duas etapas"
      description="Digite o código de 6 dígitos do seu aplicativo autenticador."
      footer="Perdeu o acesso ao aplicativo? Peça ao sócio responsável pelo escritório para redefinir o seu segundo fator."
    >
      <form
        onSubmit={(event) => {
          event.preventDefault()
          void verify(code)
        }}
        className="space-y-4"
      >
        <OtpInput
          label="Código de verificação"
          value={code}
          onChange={setCode}
          onComplete={verify}
          disabled={busy}
          invalid={Boolean(error)}
          autoFocus
          describedBy={error ? 'mfa-error' : undefined}
        />
        {error ? (
          <p id="mfa-error" role="alert" className="iu-in text-sm text-[var(--iu-danger)]">
            {error}
          </p>
        ) : null}
        <Button type="submit" className="iu-btn-lg w-full" disabled={busy || code.length !== 6} aria-busy={busy}>
          {busy ? <LoadingSpinner size="sm" decorative /> : null}
          Verificar
        </Button>
      </form>
    </AuthCard>
  )
}
