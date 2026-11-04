import {
  parseMetadata,
  toPublication,
  toPublicationDetail,
  type PublicationDetailDto,
  type PublicationListItemDto,
} from './contract'

function listItem(overrides: Partial<PublicationListItemDto> = {}): PublicationListItemDto {
  return {
    id: 'pub-1',
    processId: null,
    clienteId: null,
    clienteNome: null,
    responsavelUserId: null,
    tribunal: 'TJSP',
    cnjNumber: '1234567-48.2026.8.26.0001',
    source: 'DJEN',
    contentPreview: 'Fica a parte intimada a manifestar-se…',
    publishedAt: '2026-09-27T00:00:00+00:00',
    capturedAt: '2026-09-28T12:00:00+00:00',
    type: null,
    status: 'PendingReview',
    isRead: false,
    ...overrides,
  }
}

describe('tradução do contrato da API para o modelo da tela', () => {
  it('mapeia os campos básicos', () => {
    const publication = toPublication(listItem())

    expect(publication.id).toBe('pub-1')
    expect(publication.processNumber).toBe('1234567-48.2026.8.26.0001')
    expect(publication.journal).toBe('DJEN')
    expect(publication.publishedAt).toBe('2026-09-27T00:00:00+00:00')
  })

  it.each([
    ['PendingReview', 'untreated'],
    ['Approved', 'treated'],
    ['Corrected', 'treated'],
    ['Rejected', 'discarded'],
    ['Archived', 'archived'],
  ])('traduz o status %s da API para %s na tela', (apiStatus, expected) => {
    expect(toPublication(listItem({ status: apiStatus })).status).toBe(expected)
  })

  it('trata status desconhecido como NÃO TRATADA, nunca como resolvida', () => {
    // Num sistema de prazos, o default seguro diante de um valor não reconhecido é exibir a
    // publicação como pendente: esconder algo que talvez exija ação é o erro caro.
    expect(toPublication(listItem({ status: 'EstadoNovoQueAindaNaoConhecemos' })).status).toBe(
      'untreated'
    )
  })

  it.each([
    ['Intimacao', 'intimation'],
    ['Decisao', 'decision'],
    ['Sentenca', 'sentence'],
    ['Acordao', 'decision'],
    ['Despacho', 'other'],
    ['Outros', 'other'],
  ])('traduz o tipo %s da API para %s na tela', (apiType, expected) => {
    expect(toPublication(listItem({ type: apiType })).kind).toBe(expected)
  })

  it('usa o tipo "other" quando a API não classificou o ato', () => {
    // O tipo é nulo na captura: a classificação assistida por IA é da Fase 3.
    expect(toPublication(listItem({ type: null })).kind).toBe('other')
  })

  it('usa o trecho como conteúdo na listagem — nunca o inteiro teor', () => {
    const publication = toPublication(listItem({ contentPreview: 'apenas um trecho' }))

    expect(publication.content).toBe('apenas um trecho')
  })

  it('usa o inteiro teor no detalhe', () => {
    const detail: PublicationDetailDto = {
      ...listItem(),
      tenantId: 'tenant-1',
      externalId: '738398326',
      rawContent: 'Texto integral da comunicação processual.',
      metadataJson: '{"orgao":"1ª Vara"}',
      reviewNotes: null,
    }

    expect(toPublicationDetail(detail).content).toBe('Texto integral da comunicação processual.')
  })

  it('carrega cliente e tribunal para a tela', () => {
    const publication = toPublication(
      listItem({ clienteId: 'cli-1', clienteNome: 'Maria Silva', tribunal: 'TJRJ' })
    )

    expect(publication.clienteNome).toBe('Maria Silva')
    expect(publication.tribunal).toBe('TJRJ')
  })

  it('devolve processNumber nulo quando a API não traz número', () => {
    expect(toPublication(listItem({ cnjNumber: '' })).processNumber).toBeNull()
  })

  describe('metadados da publicação', () => {
    it('interpreta órgão, classe e link do DJEN', () => {
      const metadata = parseMetadata(
        JSON.stringify({ orgao: '1ª Vara', classe: 'Cível', link: 'https://djen.example/x' })
      )

      expect(metadata?.orgao).toBe('1ª Vara')
      expect(metadata?.classe).toBe('Cível')
      expect(metadata?.link).toBe('https://djen.example/x')
    })

    it.each([
      'javascript:alert(1)',
      'data:text/html,<b>x</b>',
      'http://inseguro.example',
      'não é url',
    ])('descarta o link %s: só https vira href', (link) => {
      expect(parseMetadata(JSON.stringify({ link }))?.link).toBeNull()
    })

    it('devolve nulo para JSON ausente, inválido ou que não é objeto', () => {
      expect(parseMetadata(null)).toBeNull()
      expect(parseMetadata('{quebrado')).toBeNull()
      expect(parseMetadata('[1,2]')).toBeNull()
    })
  })
})
