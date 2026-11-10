import type { AgendaEvento } from '@/features/agenda/types'
import { CHIPS, RESPOSTA_SEM_IA, respostaPrazosDeHoje, saudacaoInicial } from './respostas'

const prazo = (titulo: string, hora: number, extra: Partial<AgendaEvento> = {}): AgendaEvento => ({
  id: titulo,
  titulo,
  descricao: null,
  tipo: 'Prazo',
  inicio: new Date(2026, 10, 5, hora).toISOString(),
  fim: new Date(2026, 10, 5, hora + 1).toISOString(),
  diaInteiro: false,
  local: null,
  processoId: null,
  responsavelId: null,
  ...extra,
})

describe('respostas da Athena', () => {
  it('prazos de hoje: lista os da agenda real, com horário, e deixa a decisão com o advogado', () => {
    const texto = respostaPrazosDeHoje([prazo('Contestação', 9), prazo('Réplica', 18)])

    expect(texto).toContain('2 prazos vencem hoje')
    expect(texto).toContain('• Contestação, às 09:00')
    expect(texto).toContain('• Réplica, às 18:00')
    expect(texto).toContain('você decide')
  })

  it('singular, dia inteiro e nenhum prazo', () => {
    expect(respostaPrazosDeHoje([prazo('Embargos', 9)])).toContain('1 prazo vence hoje')
    expect(respostaPrazosDeHoje([prazo('Embargos', 9, { diaInteiro: true })])).toContain(
      '(dia todo)'
    )
    expect(respostaPrazosDeHoje([])).toBe('Não há prazos marcados para hoje na sua agenda.')
  })

  it('a resposta sem IA é honesta: diz que não está conectada e que nada é salvo sem revisão', () => {
    expect(RESPOSTA_SEM_IA).toMatch(/não estou conectada/)
    expect(RESPOSTA_SEM_IA).toMatch(/você decide/)
    expect(RESPOSTA_SEM_IA).toMatch(/sem a sua revisão/)
  })

  it('saudação com o nome; sem nome, sem lacuna estranha', () => {
    expect(saudacaoInicial('Ana')).toMatch(/^Olá, Ana\. /)
    expect(saudacaoInicial('')).toMatch(/^Olá\. /)
    expect(saudacaoInicial('Ana')).toContain('Eu sugiro, você decide')
  })

  it('atalhos', () => {
    expect(CHIPS).toEqual(['Prazos de hoje', 'Resumir publicação'])
  })
})
