import { detectarTipoArquivo, extensaoDe, formatTamanho } from './arquivo'

describe('detectarTipoArquivo', () => {
  it.each([
    ['peticao.pdf', 'PDF'],
    ['PETICAO.PDF', 'PDF'],
    ['contrato.docx', 'DOCX'],
    ['foto.jpg', 'Imagem'],
    ['foto.jpeg', 'Imagem'],
    ['print.png', 'Imagem'],
    ['planilha.xlsx', 'Outro'],
    ['semextensao', 'Outro'],
  ])('%s → %s', (nome, esperado) => {
    expect(detectarTipoArquivo(nome)).toBe(esperado)
  })

  it('usa a última extensão', () => {
    expect(extensaoDe('arquivo.final.pdf')).toBe('pdf')
  })
})

describe('formatTamanho', () => {
  it('escolhe a unidade pelo tamanho', () => {
    expect(formatTamanho(512)).toBe('512 B')
    expect(formatTamanho(156_000)).toBe('152 KB')
    expect(formatTamanho(2_300_000)).toBe('2,2 MB')
  })
})
