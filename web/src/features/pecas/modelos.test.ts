import { separarCitacoes } from './citacoes'
import {
  minutaComoTexto,
  montarMinuta,
  nomeDoArquivo,
  tiposPeca,
  type DadosProcesso,
} from './modelos'

const processo: DadosProcesso = {
  cnjNumber: '0000777-07.2018.8.06.0100',
  tribunal: 'TJCE',
  comarca: 'Fortaleza',
  vara: '3ª VARA CÍVEL',
  parteContraria: 'Banco Alfa S.A.',
}

describe('montarMinuta', () => {
  it.each(tiposPeca.map((t) => t.value))(
    '"%s": estrutura completa, com o processo e lacunas a preencher',
    (tipo) => {
      const minuta = montarMinuta(tipo, processo)
      const texto = minuta.paragrafos.join('\n')

      expect(minuta.paragrafos).toHaveLength(8)
      expect(texto).toContain('Processo nº 0000777-07.2018.8.06.0100')
      expect(texto).toContain('3ª VARA CÍVEL')
      expect(texto).toContain('Fortaleza')
      expect(texto).toContain('Banco Alfa S.A.')
      // Esqueleto, não peça pronta: o advogado precisa preencher e conferir.
      expect(texto).toMatch(/\[[^\]]+\]/)
      expect(texto).toContain('[Confirmar a data de início da contagem')
    }
  )

  it('cada peça cita o seu fundamento correto', () => {
    expect(montarMinuta('contestacao', processo).paragrafos.join(' ')).toContain('art. 335 do CPC')
    expect(montarMinuta('replica', processo).paragrafos.join(' ')).toContain('art. 351 do CPC')
    expect(montarMinuta('apelacao', processo).paragrafos.join(' ')).toContain('art. 1.009 do CPC')
    expect(montarMinuta('contrarrazoes', processo).paragrafos.join(' ')).toContain(
      'art. 1.010, § 1º, do CPC'
    )
  })

  it('o que o processo não informa vira lacuna, nunca um valor inventado', () => {
    const minuta = montarMinuta('contestacao', {
      cnjNumber: '1',
      tribunal: null,
      comarca: null,
      vara: null,
      parteContraria: null,
    })
    const texto = minuta.paragrafos.join('\n')

    expect(texto).toContain('[VARA]')
    expect(texto).toContain('[COMARCA]')
    expect(texto).toContain('[PARTE ADVERSA]')
  })

  it('sem comarca, usa o tribunal antes de deixar a lacuna', () => {
    const minuta = montarMinuta('contestacao', { ...processo, comarca: null })

    expect(minuta.paragrafos[0]).toContain('TJCE')
  })

  it('o prazo de 15 dias úteis vem acompanhado do aviso de conferir feriados e suspensões', () => {
    const tempestividade = montarMinuta('contestacao', processo).paragrafos[3]!

    expect(tempestividade).toContain('15 (quinze) dias úteis')
    expect(tempestividade).toMatch(/feriados ou suspensões/)
  })
})

describe('arquivo', () => {
  it('texto corrido com o título e os parágrafos separados por linha em branco', () => {
    const minuta = montarMinuta('contestacao', processo)
    const texto = minutaComoTexto(minuta)

    expect(texto.startsWith('CONTESTAÇÃO\n\n')).toBe(true)
    expect(texto.split('\n\n').length).toBe(minuta.paragrafos.length + 1)
  })

  it('o nome identifica o rascunho, a peça e o processo', () => {
    expect(nomeDoArquivo(montarMinuta('replica', processo), processo.cnjNumber)).toBe(
      'Rascunho - RÉPLICA À CONTESTAÇÃO - 0000777-07.2018.8.06.0100.txt'
    )
  })
})

describe('separarCitacoes', () => {
  const rejuntar = (texto: string) =>
    separarCitacoes(texto)
      .map((t) => t.texto)
      .join('')

  it.each([
    'art. 335 do CPC',
    'art. 1.010, § 1º, do CPC',
    'art. 85, § 11, do CPC',
    'arts. 337 e 341 do CPC',
  ])('destaca "%s"', (citacao) => {
    const trechos = separarCitacoes(`Conforme o ${citacao}, a parte deve agir.`)

    expect(trechos.filter((t) => t.citacao).map((t) => t.texto)).toEqual([citacao])
  })

  it('nunca perde nem altera texto: juntar os trechos devolve o original', () => {
    const texto = 'Nos termos do art. 335 do CPC e do art. 341 do CPC, e ainda do art. 5º solto.'

    expect(rejuntar(texto)).toBe(texto)
  })

  it('"art. 5º" sem código de lei não é destacado como citação', () => {
    expect(separarCitacoes('Veja o art. 5º do texto.').some((t) => t.citacao)).toBe(false)
  })

  it('várias citações e texto entre elas', () => {
    const trechos = separarCitacoes('A) art. 335 do CPC; B) art. 341 do CPC.')

    expect(trechos.filter((t) => t.citacao)).toHaveLength(2)
  })

  it('texto sem citação volta como um trecho comum', () => {
    expect(separarCitacoes('Sem lei aqui.')).toEqual([{ texto: 'Sem lei aqui.', citacao: false }])
  })

  it('texto vazio não produz trechos', () => {
    expect(separarCitacoes('')).toEqual([])
  })
})
