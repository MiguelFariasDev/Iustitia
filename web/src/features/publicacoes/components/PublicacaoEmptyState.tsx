import { Inbox, SearchX } from 'lucide-react'
import { EmptyState } from '@/components/shared/EmptyState'
import { Button } from '@/components/ui/button'

interface PublicacaoEmptyStateProps {
  /** true quando há filtro/busca ativo: a mensagem muda de "nada chegou" para "nada casou". */
  filtered: boolean
  onClear?: () => void
}

export function PublicacaoEmptyState({ filtered, onClear }: PublicacaoEmptyStateProps) {
  if (filtered) {
    return (
      <EmptyState
        icon={SearchX}
        title="Nenhuma publicação encontrada"
        description="Nenhuma publicação corresponde aos filtros aplicados."
        action={
          onClear ? (
            <Button variant="outline" size="sm" onClick={onClear}>
              Limpar filtros
            </Button>
          ) : undefined
        }
      />
    )
  }

  return (
    <EmptyState
      icon={Inbox}
      title="Nenhuma publicação por aqui ainda"
      description="As publicações dos processos cadastrados chegam automaticamente, a cada poucas horas. Cadastre um processo para começar a acompanhá-lo."
    />
  )
}
