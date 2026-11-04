import { FileQuestion } from 'lucide-react'
import Link from 'next/link'
import { EmptyState } from '@/components/shared/EmptyState'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'

export default function NotFound() {
  return (
    <main id="conteudo" className="flex min-h-dvh items-center justify-center p-6">
      <EmptyState
        icon={FileQuestion}
        title="Página não encontrada"
        description="O endereço acessado não existe ou foi movido."
        action={
          <Button asChild>
            <Link href={routes.dashboard}>Ir para o painel</Link>
          </Button>
        }
        className="w-full max-w-md"
      />
    </main>
  )
}
