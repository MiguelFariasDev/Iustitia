import { z } from 'zod'
import { detectarTipoArquivo, TAMANHO_MAXIMO_BYTES } from './arquivo'
import { tiposVinculo, type TipoVinculo } from './types'

/**
 * Formulário de upload. O arquivo é validado pelo NOME (extensão) e pelo tamanho; `File`
 * vem do navegador e não existe no servidor, por isso o `instanceof` só roda no cliente.
 */
export const uploadFormSchema = z
  .object({
    file: z.custom<File>((v) => typeof File !== 'undefined' && v instanceof File, {
      message: 'Selecione um arquivo.',
    }),
    vinculoTipo: z.enum(tiposVinculo as [TipoVinculo, ...TipoVinculo[]]),
    vinculoRotulo: z.string().trim().max(200, 'Use no máximo 200 caracteres.'),
  })
  .superRefine((values, ctx) => {
    const { file } = values

    if (file instanceof File) {
      if (detectarTipoArquivo(file.name) === 'Outro') {
        ctx.addIssue({
          code: 'custom',
          path: ['file'],
          message: 'Formato não aceito. Envie PDF, DOCX, PNG ou JPG.',
        })
      } else if (file.size > TAMANHO_MAXIMO_BYTES) {
        ctx.addIssue({
          code: 'custom',
          path: ['file'],
          message: 'O arquivo passa de 10 MB.',
        })
      } else if (file.size === 0) {
        ctx.addIssue({ code: 'custom', path: ['file'], message: 'O arquivo está vazio.' })
      }
    }

    if (values.vinculoTipo !== 'Avulso' && values.vinculoRotulo === '') {
      ctx.addIssue({
        code: 'custom',
        path: ['vinculoRotulo'],
        message:
          values.vinculoTipo === 'Processo'
            ? 'Informe o número do processo.'
            : 'Informe o nome do cliente.',
      })
    }
  })

export type UploadFormValues = z.infer<typeof uploadFormSchema>
