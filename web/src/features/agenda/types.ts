/** Modelo de exibição da agenda. Datas em ISO 8601 (UTC); a tela converte para o fuso local. */

export type TipoEvento = 'Prazo' | 'Audiencia' | 'Reuniao' | 'Tarefa'

export const tiposEvento: readonly TipoEvento[] = ['Prazo', 'Audiencia', 'Reuniao', 'Tarefa']

export const tipoEventoLabels: Record<TipoEvento, string> = {
  Prazo: 'Prazo',
  Audiencia: 'Audiência',
  Reuniao: 'Reunião',
  Tarefa: 'Tarefa',
}

/**
 * Cor por tipo. Sempre acompanhada do rótulo textual no chip: cor sozinha não pode ser a
 * única diferença entre tipos (acessibilidade).
 */
export const tipoEventoClasses: Record<TipoEvento, string> = {
  Prazo: 'border-destructive/40 bg-destructive/10 text-destructive',
  Audiencia: 'border-primary/40 bg-primary/10 text-primary',
  Reuniao: 'border-sky-500/40 bg-sky-500/10 text-sky-700 dark:text-sky-300',
  Tarefa: 'border-border bg-muted text-muted-foreground',
}

export interface AgendaEvento {
  id: string
  titulo: string
  descricao: string | null
  tipo: TipoEvento
  inicio: string
  fim: string
  diaInteiro: boolean
  local: string | null
  processoId: string | null
  responsavelId: string | null
}

export interface AgendaPeriodo {
  /** Início inclusivo, ISO. */
  from: string
  /** Fim exclusivo, ISO. */
  to: string
}

export type VisaoAgenda = 'mes' | 'semana' | 'dia'

export const visoesAgenda: readonly VisaoAgenda[] = ['mes', 'semana', 'dia']

export const visaoAgendaLabels: Record<VisaoAgenda, string> = {
  mes: 'Mês',
  semana: 'Semana',
  dia: 'Dia',
}
