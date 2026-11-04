import { TAMANHO_MAXIMO_BYTES } from './arquivo'
import { uploadFormSchema } from './schemas'

function arquivo(nome: string, tamanho = 1000) {
  const file = new File(['x'], nome)
  // O tamanho real vem do conteúdo; sobrescrevemos para testar o limite sem alocar 10 MB.
  Object.defineProperty(file, 'size', { value: tamanho })
  return file
}

const avulso = { vinculoTipo: 'Avulso' as const, vinculoRotulo: '' }

describe('uploadFormSchema', () => {
  it('aceita PDF avulso', () => {
    expect(uploadFormSchema.safeParse({ file: arquivo('a.pdf'), ...avulso }).success).toBe(true)
  })

  it('exige um arquivo', () => {
    const r = uploadFormSchema.safeParse({ file: undefined, ...avulso })

    expect(r.error?.issues[0]?.message).toBe('Selecione um arquivo.')
  })

  it('recusa formato que não é PDF, DOCX, PNG ou JPG', () => {
    const r = uploadFormSchema.safeParse({ file: arquivo('programa.exe'), ...avulso })

    expect(r.error?.issues[0]?.message).toMatch(/Formato não aceito/)
  })

  it('recusa arquivo acima do limite e aceita exatamente o limite', () => {
    const acima = uploadFormSchema.safeParse({
      file: arquivo('grande.pdf', TAMANHO_MAXIMO_BYTES + 1),
      ...avulso,
    })
    const limite = uploadFormSchema.safeParse({
      file: arquivo('limite.pdf', TAMANHO_MAXIMO_BYTES),
      ...avulso,
    })

    expect(acima.error?.issues[0]?.message).toBe('O arquivo passa de 10 MB.')
    expect(limite.success).toBe(true)
  })

  it('recusa arquivo vazio', () => {
    const r = uploadFormSchema.safeParse({ file: arquivo('vazio.pdf', 0), ...avulso })

    expect(r.error?.issues[0]?.message).toBe('O arquivo está vazio.')
  })

  it.each([
    ['Processo', 'Informe o número do processo.'],
    ['Cliente', 'Informe o nome do cliente.'],
  ] as const)('vínculo %s exige a referência', (vinculoTipo, mensagem) => {
    const r = uploadFormSchema.safeParse({
      file: arquivo('a.pdf'),
      vinculoTipo,
      vinculoRotulo: '  ',
    })

    expect(r.error?.issues[0]?.message).toBe(mensagem)
  })
})
