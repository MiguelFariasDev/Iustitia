'use client'

import { MessageCircle, ShieldCheck } from 'lucide-react'
import { useEffect, useState } from 'react'
import { OtpInput } from '@/components/iu/OtpInput'
import { LoadingSpinner } from '@/components/shared/LoadingSpinner'
import { Button } from '@/components/ui/button'
import { authService } from '@/lib/auth/authService'
import type { MfaEnrollment } from '@/lib/auth/types'
import { getErrorMessage } from '@/lib/api/errors'
import { StepActions } from '../StepActions'
import type { StepProps } from './types'

/** Passo 6 (opcional): WhatsApp (ainda não existe) e verificação em duas etapas (recomendada ao sócio). */
export function StepConnections({ state, onComplete, onPatch, onSkip, onBack }: StepProps) {
  const [mfaOn, setMfaOn] = useState(state.data.mfaEnabled === true)

  useEffect(() => {
    let active = true
    authService
      .hasMfaFactor()
      .then((has) => {
        if (active && has) {
          setMfaOn(true)
          if (!state.data.mfaEnabled) onPatch({ mfaEnabled: true })
        }
      })
      .catch(() => undefined)
    return () => {
      active = false
    }
    // Consulta única na entrada do passo.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return (
    <div className="space-y-5">
      <section className="iu-card p-4" aria-labelledby="wa-titulo">
        <div className="flex items-start gap-3">
          <MessageCircle className="mt-0.5 size-5 text-[var(--iu-brand)]" aria-hidden />
          <div className="flex-1">
            <h3 id="wa-titulo" className="text-sm font-semibold">
              WhatsApp para Atendimentos
            </h3>
            <p className="mt-1 text-sm text-muted-foreground">
              A IA responde o básico, e transfere para uma pessoa do escritório quando o assunto pede.
              Você acompanha as conversas em Atendimentos.
            </p>
            <p className="mt-2 text-xs text-muted-foreground">
              Estado: <strong className="text-foreground">Não conectado</strong> · a conexão ainda não está disponível (em breve).
            </p>
          </div>
          <Button type="button" variant="outline" disabled className="h-11">
            Conectar
          </Button>
        </div>
      </section>

      <section className="iu-card p-4" aria-labelledby="mfa-titulo">
        <div className="flex items-start gap-3">
          <ShieldCheck className="mt-0.5 size-5 text-[var(--iu-brand)]" aria-hidden />
          <div className="flex-1 space-y-3">
            <div>
              <h3 id="mfa-titulo" className="text-sm font-semibold">
                Verificação em duas etapas (recomendada)
              </h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Além da senha, você confirma o acesso com um código do aplicativo autenticador. Protege o sigilo
                dos seus clientes se a senha vazar.
              </p>
            </div>
            {mfaOn ? (
              <p role="status" className="iu-in text-sm font-medium text-[var(--iu-ok)]">
                ✓ Ativada
              </p>
            ) : (
              <MfaEnroll
                onEnabled={() => {
                  setMfaOn(true)
                  onPatch({ mfaEnabled: true })
                }}
              />
            )}
          </div>
        </div>
      </section>

      <StepActions onBack={onBack} onSkip={onSkip} onContinue={() => onComplete()} continueLabel="Concluir" />
    </div>
  )
}

function MfaEnroll({ onEnabled }: { onEnabled: () => void }) {
  const [enrollment, setEnrollment] = useState<MfaEnrollment | null>(null)
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function start() {
    setBusy(true)
    setError(null)
    try {
      setEnrollment(await authService.enrollMfa())
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function confirm(value: string) {
    if (!enrollment || busy || value.length !== 6) return
    setBusy(true)
    setError(null)
    try {
      await authService.confirmMfaEnrollment(enrollment.factorId, value)
      onEnabled()
    } catch (err) {
      setError(getErrorMessage(err))
      setCode('')
    } finally {
      setBusy(false)
    }
  }

  if (!enrollment) {
    return (
      <div className="space-y-2">
        <Button type="button" variant="outline" className="h-11" onClick={start} disabled={busy} aria-busy={busy}>
          {busy ? <LoadingSpinner size="sm" decorative /> : null}
          Ativar agora
        </Button>
        {error ? <p role="alert" className="text-sm text-[var(--iu-danger)]">{error}</p> : null}
      </div>
    )
  }

  return (
    <div className="iu-in space-y-3">
      <ol className="list-decimal space-y-1 pl-5 text-sm">
        <li>Abra seu aplicativo autenticador e escaneie o QR Code.</li>
        <li>Digite o código de 6 dígitos que ele mostrar.</li>
      </ol>
      <div className="flex flex-wrap items-center gap-4">
        {/* eslint-disable-next-line @next/next/no-img-element -- QR em data URI (SVG) devolvido pelo provedor */}
        <img src={enrollment.qrCode} alt="QR Code para o aplicativo autenticador" className="size-32 rounded-lg border bg-white p-1" />
        <p className="text-xs text-muted-foreground">
          Sem câmera? Digite este segredo no aplicativo:
          <br />
          <code className="iu-mono text-sm break-all text-foreground">{enrollment.secret}</code>
        </p>
      </div>
      <div className="max-w-xs">
        <OtpInput
          label="Código do aplicativo"
          value={code}
          onChange={setCode}
          onComplete={confirm}
          disabled={busy}
          invalid={Boolean(error)}
          describedBy={error ? 'mfa-enroll-error' : undefined}
        />
      </div>
      {error ? <p id="mfa-enroll-error" role="alert" className="iu-in text-sm text-[var(--iu-danger)]">{error}</p> : null}
    </div>
  )
}
