import { env } from '@/config/env'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import type { Coluna, Tarefa } from './kanban'
import { criarTarefa, lerTarefasLocais, moverTarefaPara, removerTarefa, type NovaTarefa } from './store'

interface WorkTaskDto {
  id: string
  title: string
  status: 'Todo' | 'InProgress' | 'InReview' | 'Done'
  dueDate: string | null
  processId: string | null
  assignedUserId: string | null
  createdAt: string
}

const colunaDe: Record<WorkTaskDto['status'], Coluna> = {
  Todo: 'afazer',
  InProgress: 'andamento',
  InReview: 'revisao',
  Done: 'concluido',
}

const statusDe: Record<Coluna, WorkTaskDto['status']> = {
  afazer: 'Todo',
  andamento: 'InProgress',
  revisao: 'InReview',
  concluido: 'Done',
}

/** O servidor guarda só os ids; número do processo e nome do responsável são resolvidos na tela. */
export async function listarTarefas(): Promise<Tarefa[]> {
  if (env.useMocks) return lerTarefasLocais()

  const { data } = await apiClient.get<{ items: WorkTaskDto[] }>(endpoints.tarefas.root)

  return data.items.map((t) => ({
    id: t.id,
    titulo: t.title,
    processo: '',
    processoId: t.processId,
    responsavel: '',
    responsavelId: t.assignedUserId,
    coluna: colunaDe[t.status],
    prazo: t.dueDate,
    criadaEm: t.createdAt,
  }))
}

export async function criarTarefaApi(dados: NovaTarefa): Promise<void> {
  if (env.useMocks) {
    criarTarefa(dados)

    return
  }
  await apiClient.post(endpoints.tarefas.root, {
    title: dados.titulo.trim(),
    dueDate: dados.prazo || null,
    processId: dados.processo?.id ?? null,
    assignedUserId: dados.responsavel?.id ?? null,
  })
}

export async function moverTarefaApi(id: string, destino: Coluna): Promise<void> {
  if (env.useMocks) {
    moverTarefaPara(id, destino)

    return
  }
  await apiClient.patch(endpoints.tarefas.status(id), { status: statusDe[destino] })
}

export async function removerTarefaApi(id: string): Promise<void> {
  if (env.useMocks) {
    removerTarefa(id)

    return
  }
  await apiClient.delete(endpoints.tarefas.byId(id))
}
