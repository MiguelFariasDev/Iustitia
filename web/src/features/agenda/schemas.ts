import { z } from 'zod'
import { tiposEvento, type TipoEvento } from './types'

/**
 * `inicio` e `fim` são valores de <input type="datetime-local"> (hora local, sem fuso). A
 * conversão para ISO/UTC acontece em `formToPayload`, no envio, para o formulário trabalhar
 * sempre no horário que o usuário enxerga.
 */
export const eventoFormSchema = z
  .object({
    titulo: z
      .string()
      .trim()
      .min(1, 'Informe o título do evento.')
      .max(200, 'O título pode ter no máximo 200 caracteres.'),
    tipo: z.enum(tiposEvento as [TipoEvento, ...TipoEvento[]]),
    diaInteiro: z.boolean(),
    inicio: z.string().min(1, 'Informe o início.'),
    fim: z.string(),
    local: z.string().trim().max(300, 'O local pode ter no máximo 300 caracteres.'),
    descricao: z.string().trim().max(4000, 'A descrição pode ter no máximo 4000 caracteres.'),
    processoId: z.string(),
  })
  .refine((v) => v.fim === '' || v.fim >= v.inicio, {
    path: ['fim'],
    message: 'O término não pode ser anterior ao início.',
  })

export type EventoFormValues = z.infer<typeof eventoFormSchema>

export function eventoFormDefaults(inicio: string, processoId = ''): EventoFormValues {
  return {
    titulo: '',
    tipo: 'Reuniao',
    diaInteiro: false,
    inicio,
    fim: '',
    local: '',
    descricao: '',
    processoId,
  }
}

/**
 * Valores do formulário → campos do envio. Evento de dia inteiro vai à meia-noite local do
 * dia escolhido, sem término (o servidor assume 24h).
 */
export function formToPayload(values: EventoFormValues) {
  const inicioLocal = values.diaInteiro ? `${values.inicio.slice(0, 10)}T00:00` : values.inicio

  return {
    titulo: values.titulo,
    tipo: values.tipo,
    descricao: values.descricao,
    local: values.local,
    diaInteiro: values.diaInteiro,
    inicio: new Date(inicioLocal).toISOString(),
    fim: values.diaInteiro || values.fim === '' ? null : new Date(values.fim).toISOString(),
    processoId: values.processoId === '' ? null : values.processoId,
  }
}
