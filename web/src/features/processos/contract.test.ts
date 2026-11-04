import { toProcesso, toProcessoListItem, type ProcessoDto } from './contract'

function dto(overrides: Partial<ProcessoDto> = {}): ProcessoDto {
  return {
    id: 'proc-1',
    cnjNumber: '1234567-48.2026.8.26.0001',
    clienteId: 'cli-1',
    responsavelUserId: 'user-1',
    area: 'Civel',
    status: 'Ativo',
    tribunal: 'TJSP',
    comarca: 'São Paulo',
    vara: '1ª Vara Cível',
    assunto: 'Cobrança',
    parteContraria: 'Empresa X',
    valorCausa: 15000.5,
    dataDistribuicao: '2026-01-15',
    observacoes: null,
    geraCaptura: true,
    createdAt: '2026-09-29T12:00:00+00:00',
    ...overrides,
  }
}

describe('tradução do processo da API para a tela', () => {
  it('mapeia os campos', () => {
    const processo = toProcesso(dto())

    expect(processo.cnjNumber).toBe('1234567-48.2026.8.26.0001')
    expect(processo.area).toBe('Civel')
    expect(processo.status).toBe('Ativo')
    expect(processo.valorCausa).toBe(15000.5)
    expect(processo.geraCaptura).toBe(true)
  })

  it('trata status desconhecido como ATIVO, nunca como arquivado', () => {
    // Exibir como arquivado esconderia da advogada um processo que talvez tenha prazo
    // correndo — o erro caro num sistema jurídico.
    expect(toProcesso(dto({ status: 'EstadoNovo' })).status).toBe('Ativo')
  })

  it('trata área desconhecida como Outros', () => {
    // Área errada só desloca um filtro; não esconde nada.
    expect(toProcesso(dto({ area: 'AreaNova' })).area).toBe('Outros')
  })

  it.each(['Suspenso', 'Encerrado', 'Arquivado'])('preserva o status conhecido %s', (status) => {
    expect(toProcesso(dto({ status })).status).toBe(status)
  })

  it('traduz o item de listagem com as mesmas regras', () => {
    const item = toProcessoListItem({
      id: 'proc-2',
      cnjNumber: '7654321-87.2025.8.19.0500',
      clienteId: 'cli-2',
      area: 'Inexistente',
      status: 'Inexistente',
      tribunal: 'TJRJ',
      parteContraria: null,
      dataDistribuicao: null,
    })

    expect(item.area).toBe('Outros')
    expect(item.status).toBe('Ativo')
  })
})
