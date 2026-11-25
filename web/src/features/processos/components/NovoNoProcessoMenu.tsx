'use client'

import {
  CalendarClock,
  CalendarPlus,
  ChevronDown,
  FilePlus2,
  Gavel,
  ListChecks,
  MessagesSquare,
  Plus,
  type LucideIcon,
} from 'lucide-react'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { EventoDialog, type EventoDialogState } from '@/features/agenda/components/EventoDialog'
import { DocumentoUploadModal } from '@/features/documentos/components/DocumentoUploadModal'
import { NovaTarefaDialog } from '@/features/tarefas/components/NovaTarefaDialog'

import { opcoesNoProcesso, type OpcaoNoProcesso } from '@/components/layout/createActions'

export type NovoNoProcesso = OpcaoNoProcesso

const ehOpcao = (valor: string | null): valor is NovoNoProcesso =>
  opcoesNoProcesso.some((opcao) => opcao.id === valor)

const opcoes: readonly { id: NovoNoProcesso; label: string; icon: LucideIcon }[] = [
  { id: 'tarefa', label: 'Tarefa', icon: ListChecks },
  { id: 'evento', label: 'Evento', icon: CalendarPlus },
  { id: 'prazo', label: 'Prazo', icon: CalendarClock },
  { id: 'audiencia', label: 'Audiência', icon: Gavel },
  { id: 'atendimento', label: 'Atendimento', icon: MessagesSquare },
  { id: 'documento', label: 'Documento', icon: FilePlus2 },
]

/**
 * Diálogo da agenda já com o processo e o tipo escolhidos. Prazo nasce "dia inteiro" (vence no
 * dia, não numa hora); atendimento é um compromisso com o cliente, registrado como reunião.
 */
export function presetDoEvento(
  tipo: Exclude<NovoNoProcesso, 'tarefa' | 'documento'>,
  processoId: string,
  clienteNome: string | null
): EventoDialogState {
  switch (tipo) {
    case 'prazo':
      return { processoId, tipo: 'Prazo', diaInteiro: true }
    case 'audiencia':
      return { processoId, tipo: 'Audiencia' }
    case 'atendimento':
      return {
        processoId,
        tipo: 'Reuniao',
        titulo: clienteNome ? `Atendimento — ${clienteNome}` : 'Atendimento ao cliente',
      }
    default:
      return { processoId, tipo: 'Reuniao' }
  }
}

/**
 * "Novo" na tela do processo: tudo o que se cria aqui já nasce ligado a ele — tarefa, evento,
 * prazo, audiência, atendimento (na agenda) e documento. Também abre pelo "Novo › Neste processo"
 * da barra superior, que chega como `?novo=prazo`: o diálogo aberto é derivado da URL, e fechar
 * limpa o parâmetro (recarregar a página não reabre).
 */
export function NovoNoProcessoMenu({
  processoId,
  cnjNumber,
  clienteNome,
}: {
  processoId: string
  cnjNumber: string
  clienteNome: string | null
}) {
  const searchParams = useSearchParams()
  const router = useRouter()
  const pathname = usePathname()
  const pedido = searchParams.get('novo')
  const pedidoDaUrl = ehOpcao(pedido) ? pedido : null
  const [escolhido, setEscolhido] = useState<NovoNoProcesso | null>(null)
  const ativo = escolhido ?? pedidoDaUrl

  function fechar() {
    setEscolhido(null)
    if (pedidoDaUrl) router.replace(pathname as never, { scroll: false })
  }

  const evento =
    ativo && ativo !== 'tarefa' && ativo !== 'documento'
      ? presetDoEvento(ativo, processoId, clienteNome)
      : null

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button size="sm" aria-label="Criar novo item neste processo">
            <Plus aria-hidden />
            Novo
            <ChevronDown aria-hidden />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-52">
          <DropdownMenuLabel>Novo neste processo</DropdownMenuLabel>
          {opcoes.map(({ id, label, icon: Icon }) => (
            <DropdownMenuItem key={id} onSelect={() => setEscolhido(id)}>
              <Icon aria-hidden />
              {label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>

      <EventoDialog state={evento} onClose={fechar} />
      <NovaTarefaDialog
        open={ativo === 'tarefa'}
        onClose={fechar}
        processo={{ id: processoId, cnjNumber }}
      />
      {ativo === 'documento' ? (
        <DocumentoUploadModal
          open
          onOpenChange={(aberto) => (aberto ? undefined : fechar())}
          vinculo={{ tipo: 'Processo', rotulo: cnjNumber }}
        />
      ) : null}
    </>
  )
}
