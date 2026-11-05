'use client'

import { MailCheck } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { authService } from '@/lib/auth/authService'
import { getErrorMessage } from '@/lib/api/errors'
import { notify } from '@/lib/notify'
import { AuthCard } from './AuthCard'

export const RESEND_COOLDOWN_SECONDS = 30

interface VerifyEmailNoticeProps {
  email: string
  onChangeEmail: () => void
}

/**
 * "Confirme seu e-mail". O texto é NEUTRO de propósito: o mesmo para e-mail novo e já cadastrado,
 * para o cadastro não servir de oráculo de contas existentes.
 */
export function VerifyEmailNotice({ email, onChangeEmail }: VerifyEmailNoticeProps) {
  const [seconds, setSeconds] = useState(RESEND_COOLDOWN_SECONDS)
  const [sending, setSending] = useState(false)

  useEffect(() => {
    if (seconds <= 0) return
    const timer = setTimeout(() => setSeconds((s) => s - 1), 1000)
    return () => clearTimeout(timer)
  }, [seconds])

  async function resend() {
    setSending(true)
    try {
      await authService.resendConfirmation(email)
      notify.success('E-mail reenviado', { description: 'Confira também a caixa de spam.' })
      setSeconds(RESEND_COOLDOWN_SECONDS)
    } catch (err) {
      notify.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <AuthCard title="Confirme seu e-mail">
      <div role="status" className="iu-card flex gap-3 p-4">
        <MailCheck className="mt-0.5 size-5 shrink-0 text-[var(--iu-ok)]" aria-hidden />
        <p className="text-sm">
          Se <strong className="break-all">{email}</strong> for um e-mail novo, enviamos o link de
          confirmação. Depois de confirmar, entre para configurar o escritório.
        </p>
      </div>
      <div className="flex flex-col gap-2 sm:flex-row">
        <Button
          type="button"
          variant="outline"
          className="h-11 flex-1"
          onClick={resend}
          disabled={seconds > 0 || sending}
          aria-live="polite"
        >
          {seconds > 0 ? `Reenviar em ${seconds}s` : 'Reenviar e-mail'}
        </Button>
        <Button type="button" variant="ghost" className="h-11 flex-1" onClick={onChangeEmail}>
          Corrigir e-mail
        </Button>
      </div>
    </AuthCard>
  )
}
