import { useQuery, useQueryClient } from '@tanstack/react-query'
import { processosQuery } from '@/features/processos/queries'
import { listUsers } from '@/features/settings/api'
import { notify } from '@/lib/notify'
import { queryKeys } from '@/lib/query/queryKeys'
import { criarTarefaApi, listarTarefas, moverTarefaApi, removerTarefaApi } from './api'
import { moverTarefa, type Coluna, type Tarefa } from './kanban'
import type { NovaTarefa } from './store'

const CHAVE = ['tarefas'] as const

/** Tarefas com o número do processo e o nome do responsável já preenchidos. */
export function useTarefas(): Tarefa[] {
  const { data } = useQuery({ queryKey: CHAVE, queryFn: listarTarefas, staleTime: 30_000 })
  const { data: processos } = useQuery(processosQuery({ pageSize: 100 }))
  const { data: usuarios } = useQuery({ queryKey: queryKeys.users.list(), queryFn: listUsers })

  return (data ?? []).map((tarefa) => ({
    ...tarefa,
    processo:
      tarefa.processo ||
      (processos?.items.find((p) => p.id === tarefa.processoId)?.cnjNumber ?? ''),
    responsavel:
      tarefa.responsavel || (usuarios?.find((u) => u.id === tarefa.responsavelId)?.name ?? ''),
  }))
}

export function useTarefaActions() {
  const queryClient = useQueryClient()
  const recarregar = () => queryClient.invalidateQueries({ queryKey: CHAVE })

  async function criar(dados: NovaTarefa): Promise<boolean> {
    try {
      await criarTarefaApi(dados)
      await recarregar()

      return true
    } catch {
      notify.error('Não foi possível criar a tarefa.')

      return false
    }
  }

  async function mover(id: string, destino: Coluna) {
    queryClient.setQueryData<Tarefa[]>(CHAVE, (atual) => moverTarefa(atual ?? [], id, destino))
    try {
      await moverTarefaApi(id, destino)
    } catch {
      notify.error('Não foi possível mover a tarefa.')
      await recarregar()
    }
  }

  async function remover(id: string) {
    try {
      await removerTarefaApi(id)
      await recarregar()
    } catch {
      notify.error('Não foi possível excluir a tarefa.')
    }
  }

  return { criar, mover, remover }
}
