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
 * Nome do que está sendo criado/editado, no gênero certo: "Nova audiência", "Novo prazo"… O diálogo e o
 * botão de salvar usam estes nomes para o card nunca dizer "Novo evento" quando é uma audiência.
 * Reunião é o evento genérico da agenda; atendimento é uma reunião com o cliente.
 */
export interface NomeDoEvento {
  novo: string
  editar: string
  criar: string
  excluir: string
}

const nomesPorTipo: Record<TipoEvento, NomeDoEvento> = {
  Prazo: { novo: 'Novo prazo', editar: 'Editar prazo', criar: 'Criar prazo', excluir: 'Excluir prazo?' },
  Audiencia: {
    novo: 'Nova audiência',
    editar: 'Editar audiência',
    criar: 'Criar audiência',
    excluir: 'Excluir audiência?',
  },
  Reuniao: { novo: 'Novo evento', editar: 'Editar evento', criar: 'Criar evento', excluir: 'Excluir evento?' },
  Tarefa: { novo: 'Nova tarefa', editar: 'Editar tarefa', criar: 'Criar tarefa', excluir: 'Excluir tarefa?' },
}

const nomeAtendimento: NomeDoEvento = {
  novo: 'Novo atendimento',
  editar: 'Editar atendimento',
  criar: 'Criar atendimento',
  excluir: 'Excluir atendimento?',
}

export function nomeDoEvento(tipo: TipoEvento, atendimento = false): NomeDoEvento {
  return atendimento && tipo === 'Reuniao' ? nomeAtendimento : nomesPorTipo[tipo]
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
  /** ISO de quando foi dado como concluído (prazo cumprido, audiência realizada…); ausente/`null` = em aberto. */
  concluidoEm?: string | null
}

export const estaConcluido = (evento: Pick<AgendaEvento, 'concluidoEm'>) => Boolean(evento.concluidoEm)

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
