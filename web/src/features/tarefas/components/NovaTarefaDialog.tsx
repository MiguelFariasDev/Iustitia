'use client'

import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ProcessoSelect } from '@/features/agenda/components/ProcessoSelect'
import { ResponsavelSelect } from '@/features/agenda/components/ResponsavelSelect'
import { processosQuery } from '@/features/processos/queries'
import { listUsers } from '@/features/settings/api'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { useTarefaActions } from '../hooks'

interface NovaTarefaDialogProps {
  open: boolean
  onClose: () => void
  /** Processo já escolhido (tarefa criada na tela do caso); fixo, não dá para trocar. */
  processo?: { id: string; cnjNumber: string }
}

export function NovaTarefaDialog({ open, onClose, processo }: NovaTarefaDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(aberto) => (aberto ? undefined : onClose())}>
      {open ? <Corpo onClose={onClose} processoFixo={processo} /> : null}
    </Dialog>
  )
}

function Corpo({ onClose, processoFixo }: { onClose: () => void; processoFixo?: { id: string; cnjNumber: string } }) {
  const [titulo, setTitulo] = useState('')
  const [prazo, setPrazo] = useState('')
  const [processoId, setProcessoId] = useState(processoFixo?.id ?? '')
  const [responsavelId, setResponsavelId] = useState('')
  const [tentou, setTentou] = useState(false)
  const { criar } = useTarefaActions()

  const { data: processos } = useQuery(processosQuery({ pageSize: 100, status: 'Ativo' }))
  const { data: usuarios } = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers })

  async function salvar(event: React.FormEvent) {
    event.preventDefault()
    setTentou(true)
    if (titulo.trim() === '') return

    const processo = processoFixo ?? processos?.items.find((p) => p.id === processoId)
    const usuario = usuarios?.find((u) => u.id === responsavelId)
    const criada = await criar({
      titulo,
      prazo: prazo || null,
      processo: processoId
        ? { id: processoId, cnjNumber: processo?.cnjNumber ?? '' }
        : null,
      responsavel: usuario ? { id: usuario.id, nome: usuario.name } : null,
    })
    if (!criada) return
    notify.success('Tarefa criada.')
    onClose()
  }

  return (
    <DialogContent className="sm:max-w-md">
      <DialogHeader>
        <DialogTitle>Nova tarefa</DialogTitle>
        <DialogDescription>
          A tarefa fica visível para toda a equipe do escritório.
        </DialogDescription>
      </DialogHeader>

      <form onSubmit={salvar} className="space-y-4" noValidate>
        <div className="space-y-1.5">
          <Label htmlFor="tarefa-titulo">Título</Label>
          <Input
            id="tarefa-titulo"
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            maxLength={200}
            autoComplete="off"
            aria-invalid={tentou && titulo.trim() === ''}
          />
          {tentou && titulo.trim() === '' ? (
            <p role="alert" className="text-sm text-destructive">
              Informe o título da tarefa.
            </p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="tarefa-prazo">Prazo</Label>
          <Input
            id="tarefa-prazo"
            type="date"
            value={prazo}
            onChange={(e) => setPrazo(e.target.value)}
          />
        </div>

        <div className="space-y-1.5">
          <Label>Processo</Label>
          <ProcessoSelect
            value={processoId}
            onChange={setProcessoId}
            disabled={Boolean(processoFixo)}
          />
        </div>

        <div className="space-y-1.5">
          <Label>Responsável</Label>
          <ResponsavelSelect
            value={responsavelId}
            onChange={setResponsavelId}
            emptyLabel="Sem responsável"
            label="Responsável pela tarefa"
          />
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit">Criar tarefa</Button>
        </DialogFooter>
      </form>
    </DialogContent>
  )
}
