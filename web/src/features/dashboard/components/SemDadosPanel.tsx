import Link from 'next/link'
import type { Route } from 'next'
import { Button } from '@/components/ui/button'

/**
 * Painel de um perfil cujo módulo ainda não tem backend. Em vez de números de exemplo — que
 * quem olha o painel tomaria por reais —, diz o que falta e leva ao que já funciona.
 */
export function SemDadosPanel({
  titulo,
  descricao,
  acao,
}: {
  titulo: string
  descricao: string
  acao?: { href: Route; label: string }
}) {
  return (
    <div className="flex flex-col items-center gap-4 rounded-xl border border-dashed px-6 py-14 text-center">
      <div className="max-w-md space-y-2">
        <h2 className="text-lg font-semibold">{titulo}</h2>
        <p className="text-sm text-muted-foreground">{descricao}</p>
      </div>
      {acao ? (
        <Button asChild variant="outline">
          <Link href={acao.href}>{acao.label}</Link>
        </Button>
      ) : null}
    </div>
  )
}
