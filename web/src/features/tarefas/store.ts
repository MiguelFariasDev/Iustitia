import { create } from 'zustand'
import { moverTarefa, type Coluna, type Tarefa } from './kanban'

const dia = (deslocamento: number) => {
  const d = new Date()
  d.setDate(d.getDate() + deslocamento)

  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}
const criada = (deslocamento: number) => {
  const d = new Date()
  d.setDate(d.getDate() + deslocamento)

  return d.toISOString()
}

/**
 * TAREFAS DE DEMONSTRAÇÃO. Não existe módulo de tarefas no backend: este é um repositório em
 * memória (zerado ao recarregar a página), com datas relativas a hoje para a tela nunca ficar
 * desatualizada.
 */
export function tarefasIniciais(): Tarefa[] {
  const t = (
    id: string,
    titulo: string,
    processo: string,
    responsavel: string,
    coluna: Coluna,
    prazoEm: number | null,
    criadaHa: number
  ): Tarefa => ({
    id,
    titulo,
    processo,
    responsavel,
    coluna,
    prazo: prazoEm === null ? null : dia(prazoEm),
    criadaEm: criada(-criadaHa),
  })

  return [
    t('t1', 'Protocolar contestação', '0000777-07.2018.8.06.0100', 'Marina Lopes', 'afazer', 2, 10),
    t('t2', 'Juntar procuração', '0001234-55.2025.8.06.0001', 'Carla Dias', 'afazer', 14, 3),
    t('t3', 'Preparar réplica', '0005550-12.2024.8.06.0001', 'Bruno Lima', 'andamento', 0, 12),
    t(
      't4',
      'Calcular honorários de êxito',
      '0000777-07.2018.8.06.0100',
      'Marina Lopes',
      'andamento',
      9,
      6
    ),
    t('t5', 'Revisar minuta de acordo', '0003210-77.2023.8.06.0001', 'Bruno Lima', 'revisao', 1, 8),
    t(
      't6',
      'Conferir cálculos do perito',
      '0001234-55.2025.8.06.0001',
      'Rafael Nunes',
      'revisao',
      5,
      4
    ),
    t(
      't7',
      'Enviar documentos ao cliente',
      '0005550-12.2024.8.06.0001',
      'Carla Dias',
      'concluido',
      -1,
      9
    ),
    t(
      't8',
      'Organizar pasta do processo',
      '0003210-77.2023.8.06.0001',
      'Rafael Nunes',
      'concluido',
      null,
      5
    ),
  ]
}

interface TarefasState {
  tarefas: Tarefa[]
  mover: (id: string, destino: Coluna) => void
  limpar: () => void
}

export const useTarefasStore = create<TarefasState>((set) => ({
  tarefas: tarefasIniciais(),
  mover: (id, destino) => set((s) => ({ tarefas: moverTarefa(s.tarefas, id, destino) })),
  limpar: () => set({ tarefas: tarefasIniciais() }),
}))
