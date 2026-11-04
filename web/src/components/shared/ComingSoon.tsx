import type { LucideIcon } from 'lucide-react'
import Link from 'next/link'
import { PageHeader } from '@/components/shared/PageHeader'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'

interface ComingSoonProps {
  title: string
  description: string
  icon: LucideIcon
  /** Fase do roadmap em que o módulo entra (ex.: "Fase 2"). */
  phase: string
  /** O que o módulo vai entregar — dá contexto a quem chega pela navegação. */
  highlights: string[]
}

/**
 * Placeholder de módulo ainda não implementado. A rota existe desde a Etapa 0.8 para
 * que a navegação seja completa e os links nunca levem a um 404.
 */
export function ComingSoon({ title, description, icon: Icon, phase, highlights }: ComingSoonProps) {
  return (
    <>
      <PageHeader title={title} description={description} />
      <div className="flex flex-col items-center gap-6 rounded-xl border border-dashed px-6 py-14 text-center">
        <span className="flex size-14 items-center justify-center rounded-2xl bg-secondary/10 text-secondary">
          <Icon className="size-7" aria-hidden />
        </span>
        <div className="max-w-md space-y-2">
          <h2 className="text-lg font-semibold">Disponível na {phase}</h2>
          <p className="text-sm text-muted-foreground">
            Este módulo está no roteiro do produto. Enquanto isso, o que já funciona está na área de
            trabalho.
          </p>
        </div>
        <ul className="max-w-md space-y-1.5 text-left text-sm text-muted-foreground">
          {highlights.map((item) => (
            <li key={item} className="flex gap-2">
              <span aria-hidden className="text-secondary">
                •
              </span>
              {item}
            </li>
          ))}
        </ul>
        <Button asChild variant="outline">
          <Link href={routes.dashboard}>Voltar para a área de trabalho</Link>
        </Button>
      </div>
    </>
  )
}
