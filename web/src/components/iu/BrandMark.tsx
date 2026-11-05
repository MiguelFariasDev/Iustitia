import { Sparkles } from 'lucide-react'
import { env } from '@/config/env'
import { cn } from '@/lib/utils'

interface BrandMarkProps {
  className?: string
  /** `light` sobre fundos escuros (painel lateral). */
  tone?: 'default' | 'light'
}

/**
 * Marca do v4: quadrado 22px, raio 7px, fundo #34d399 e faíscas em #0c2b24 + nome do produto.
 * Os hex da marca são os do v4 (a marca é um ativo fixo, não um token de tema); o nome vem de
 * configuração (Marca Desacoplada — ADR-023).
 */
export function BrandMark({ className, tone = 'default' }: BrandMarkProps) {
  return (
    <span className={cn('inline-flex items-center gap-2 font-semibold tracking-tight', className)}>
      <span
        aria-hidden
        className="flex size-[22px] items-center justify-center rounded-[7px] bg-[#34d399] text-[#0c2b24]"
      >
        <Sparkles className="size-3.5" strokeWidth={1.75} />
      </span>
      <span className={tone === 'light' ? 'text-white' : undefined}>{env.appName}</span>
    </span>
  )
}
