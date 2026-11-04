import { toContactPayload, toContato, type ContactDto } from './contract'

const dto: ContactDto = {
  id: 'c1',
  type: 'Testemunha',
  name: 'João Pereira',
  document: '111.444.777-35',
  email: 'joao@email.com',
  phone: '(85) 99999-1234',
  notes: null,
  isActive: true,
  createdAt: '2026-11-01T10:00:00Z',
}

describe('contrato de contatos', () => {
  it('traduz o DTO do backend para o modelo da tela', () => {
    expect(toContato(dto)).toMatchObject({
      nome: 'João Pereira',
      tipo: 'Testemunha',
      documento: '111.444.777-35',
      telefone: '(85) 99999-1234',
    })
  })

  it('tipo desconhecido cai em Outros', () => {
    expect(toContato({ ...dto, type: 'Perito' }).tipo).toBe('Outros')
  })

  it('o payload usa os nomes do backend e troca campos vazios por null', () => {
    expect(
      toContactPayload({
        nome: ' Maria ',
        tipo: 'Fornecedor',
        documento: '',
        email: '',
        telefone: '(11) 98765-4321',
        observacoes: '  ',
      })
    ).toEqual({
      type: 'Fornecedor',
      name: 'Maria',
      document: null,
      email: null,
      phone: '(11) 98765-4321',
      notes: null,
    })
  })
})
