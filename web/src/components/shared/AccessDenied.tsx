import { ShieldAlert } from 'lucide-react'
import Link from 'next/link'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'

/**
 * Tela mostrada quando o usuário abre, por endereço digitado ou link antigo, uma tela que o
 * papel ou o tipo de conta dele não alcança. É aviso de interface: a API já recusaria os dados.
 */
export function AccessDenied() {
  return (
    <div
      role="alert"
      className="mx-auto flex max-w-md flex-col items-center gap-4 py-16 text-center"
    >
      <ShieldAlert className="size-10 text-muted-foreground" aria-hidden />
      <h1 className="text-xl font-semibold">Você não tem acesso a esta tela</h1>
      <p className="text-sm text-muted-foreground">
        Seu perfil não inclui esta área. Se precisar dela, peça a quem administra a conta para
        ajustar o seu papel.
      </p>
      <Button asChild>
        <Link href={routes.dashboard}>Voltar à área de trabalho</Link>
      </Button>
    </div>
  )
}
