'use client'

import { ShieldCheck } from 'lucide-react'
import { useRouter, useSearchParams } from 'next/navigation'
import { useEffect, useState } from 'react'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { safeRedirectPath } from '@/config/routes'
import { authService } from '@/lib/auth/authService'
import { AuthError, type MfaEnrollment } from '@/lib/auth/types'
import { reportError } from '@/lib/observability/reportError'
import { AuthCard } from './AuthCard'

type Stage =
  | { kind: 'loading' }
  | { kind: 'enroll'; enrollment: MfaEnrollment }
  | { kind: 'challenge'; factorId: string }
  | { kind: 'error' }

/**
 * Segundo fator (TOTP) para quem tem papel privilegiado. Se a pessoa ainda não cadastrou um
 * autenticador, mostra o QR code; se já cadastrou, só pede o código de 6 dígitos. Em ambos os
 * casos, confirmar eleva a sessão a aal2 e a API deixa de responder AUTH_MFA_REQUIRED.
 */
export function MfaForm() {
  const router = useRouter()
  const params = useSearchParams()
  const [stage, setStage] = useState<Stage>({ kind: 'loading' })
  const [code, setCode] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let active = true
    async function start() {
      try {
        const status = await authService.mfaStatus()
        if (!active) return
        if (status.enrolled && status.factorId) {
          setStage({ kind: 'challenge', factorId: status.factorId })
        } else {
          setStage({ kind: 'enroll', enrollment: await authService.mfaEnroll() })
        }
      } catch (error) {
        reportError(error)
        if (active) setStage({ kind: 'error' })
      }
    }
    void start()
    return () => {
      active = false
    }
  }, [])

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (stage.kind !== 'enroll' && stage.kind !== 'challenge') return
    const factorId = stage.kind === 'enroll' ? stage.enrollment.factorId : stage.factorId
    setBusy(true)
    setMessage(null)
    try {
      await authService.mfaVerify(factorId, code.trim())
      // Hard navigation: descarta o cache em memória, que foi montado com a sessão aal1.
      window.location.assign(safeRedirectPath(params.get('next')))
    } catch (error) {
      setMessage(
        error instanceof AuthError ? error.message : 'Não foi possível verificar o código.'
      )
      setCode('')
      if (!(error instanceof AuthError)) reportError(error)
    } finally {
      setBusy(false)
    }
  }

  if (stage.kind === 'loading') {
    return (
      <div className="flex justify-center py-12">
        <LoadingSpinner label="Preparando verificação…" />
      </div>
    )
  }

  if (stage.kind === 'error') {
    return (
      <AuthCard title="Verificação em duas etapas">
        <p role="alert" className="text-sm text-destructive">
          Não foi possível iniciar a verificação. Tente novamente em instantes.
        </p>
        <Button onClick={() => router.refresh()}>Tentar de novo</Button>
      </AuthCard>
    )
  }

  const enrolling = stage.kind === 'enroll'

  return (
    <AuthCard
      title="Verificação em duas etapas"
      description={
        enrolling
          ? 'Sócios e titulares precisam de um segundo fator. Leia o QR code no seu aplicativo autenticador (Google Authenticator, Authy, 1Password…) e digite o código de 6 dígitos.'
          : 'Digite o código de 6 dígitos do seu aplicativo autenticador.'
      }
    >
      <form onSubmit={submit} className="space-y-5" noValidate>
        {enrolling ? (
          <div className="flex flex-col items-center gap-3 rounded-xl border bg-card p-4">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              src={stage.enrollment.qrCode}
              alt="QR code para cadastrar o aplicativo autenticador"
              width={176}
              height={176}
            />
            <p className="text-center text-xs text-muted-foreground">
              Sem câmera? Digite esta chave no aplicativo:
              <br />
              <code className="font-mono text-foreground select-all">
                {stage.enrollment.secret}
              </code>
            </p>
          </div>
        ) : null}

        <div className="space-y-2">
          <Label htmlFor="mfa-code">Código de 6 dígitos</Label>
          <Input
            id="mfa-code"
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]*"
            maxLength={6}
            value={code}
            onChange={(event) => setCode(event.target.value.replace(/\D/g, ''))}
            aria-invalid={message ? true : undefined}
            aria-describedby={message ? 'mfa-error' : undefined}
            className="text-center font-mono text-lg tracking-[0.4em]"
            autoFocus
          />
          {message ? (
            <p id="mfa-error" role="alert" className="text-sm text-destructive">
              {message}
            </p>
          ) : null}
        </div>

        <Button type="submit" className="w-full" disabled={busy || code.length !== 6}>
          {busy ? <LoadingSpinner /> : <ShieldCheck aria-hidden />}
          {enrolling ? 'Ativar e continuar' : 'Verificar'}
        </Button>
      </form>
    </AuthCard>
  )
}
