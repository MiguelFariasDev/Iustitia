import { criarArmazem } from '@/lib/storage/armazem'
import { moverTarefa, type Coluna, type Tarefa } from './kanban'

/**
 * Tarefas guardadas NESTE navegador, por usuário (ver lib/storage/armazem.ts). Só vale no modo
 * de mocks: com servidor, as tarefas vêm da API (api.ts). Começa vazio — sem tarefa de exemplo.
 */
const armazem = criarArmazem<{ tarefas: Tarefa[] }>('iustitia:tarefas:v1:', { tarefas: [] })

export const lerTarefasLocais = () => armazem.ler().tarefas

export interface NovaTarefa {
  titulo: string
  prazo: string | null
  processo?: { id: string; cnjNumber: string } | null
  responsavel?: { id: string; nome: string } | null
}

export function criarTarefa(dados: NovaTarefa): Tarefa {
  const tarefa: Tarefa = {
    id: crypto.randomUUID(),
    titulo: dados.titulo.trim(),
    processo: dados.processo?.cnjNumber ?? '',
    processoId: dados.processo?.id ?? null,
    responsavel: dados.responsavel?.nome ?? '',
    responsavelId: dados.responsavel?.id ?? null,
    coluna: 'afazer',
    prazo: dados.prazo || null,
    criadaEm: new Date().toISOString(),
  }
  armazem.gravar({ tarefas: [...armazem.ler().tarefas, tarefa] })

  return tarefa
}

export function moverTarefaPara(id: string, destino: Coluna) {
  armazem.gravar({ tarefas: moverTarefa(armazem.ler().tarefas, id, destino) })
}

export function removerTarefa(id: string) {
  armazem.gravar({ tarefas: armazem.ler().tarefas.filter((t) => t.id !== id) })
}

/** Só para testes: zera o armazém. */
export function limparTarefas() {
  armazem.gravar({ tarefas: [] })
}
