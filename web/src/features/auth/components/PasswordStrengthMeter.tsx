import { passwordStrength } from '@/lib/security/passwordStrength'
import { cn } from '@/lib/utils'

const tone = [
  'bg-[var(--iu-danger)]',
  'bg-[var(--iu-danger)]',
  'bg-[var(--iu-warn)]',
  'bg-[var(--iu-ok)]',
  'bg-[var(--iu-ok)]',
]

/** Medidor de força: o estado vai em TEXTO (não só cor) e é anunciado com aria-live. */
export function PasswordStrengthMeter({ password }: { password: string }) {
  const { score, label } = passwordStrength(password)
  if (!password) {
    return (
      <p className="text-xs text-muted-foreground">
        Dica: uma frase com 3 ou 4 palavras (ex.: “cavalo bateria grampo certo”) é longa, fácil de
        lembrar e difícil de adivinhar.
      </p>
    )
  }

  return (
    <div className="space-y-1.5">
      <div className="flex gap-1" aria-hidden>
        {[0, 1, 2, 3].map((index) => (
          <span
            key={index}
            className={cn('h-1 flex-1 rounded-full bg-border transition-colors', index < score && tone[score])}
          />
        ))}
      </div>
      <p className="text-xs text-muted-foreground" aria-live="polite">
        Força da senha: <strong className="text-foreground">{label}</strong>
      </p>
    </div>
  )
}
