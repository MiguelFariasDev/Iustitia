import { publicationsToCsv } from './export'
import type { Publication } from './types'

function publication(overrides: Partial<Publication> = {}): Publication {
  return {
    id: 'p1',
    publishedAt: '2026-09-27T12:00:00.000Z',
    capturedAt: '2026-09-28T12:00:00.000Z',
    kind: 'intimation',
    processId: 'proc-1',
    processNumber: '1234567-48.2026.8.26.0001',
    tribunal: 'TJSP',
    journal: 'DJEN',
    clienteId: 'c1',
    clienteNome: 'Maria Silva',
    status: 'untreated',
    isRead: false,
    content: 'TEXTO-SIGILOSO-DA-PUBLICACAO',
    reviewNotes: null,
    metadata: null,
    ...overrides,
  }
}

describe('exportação de publicações em CSV', () => {
  it('leva os dados de controle da planilha', () => {
    const [header, row] = publicationsToCsv([publication()]).slice(1).split('\r\n')

    expect(header).toBe('Divulgado em;Tipo;Processo;Tribunal;Cliente;Status;Lida')
    expect(row).toContain('Intimação')
    expect(row).toContain('1234567-48.2026.8.26.0001')
    expect(row).toContain('TJSP')
    expect(row).toContain('Maria Silva')
    expect(row).toContain('Não tratada')
    expect(row?.endsWith(';Não')).toBe(true)
  })

  it('NUNCA exporta o texto da publicação (sigilo profissional)', () => {
    expect(publicationsToCsv([publication()])).not.toContain('SIGILOSO')
  })

  it('neutraliza um nome de cliente que viraria fórmula', () => {
    const csv = publicationsToCsv([publication({ clienteNome: '=HYPERLINK("http://x")' })])

    expect(csv).toContain("'=HYPERLINK")
  })
})
