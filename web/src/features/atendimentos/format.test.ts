import { displayName, formatPhone, initials } from './format'

describe('formatPhone', () => {
  it.each([
    ['5585999998888', '+55 (85) 99999-8888'],
    ['558533334444', '+55 (85) 3333-4444'],
    ['14155552671', '+14155552671'],
    ['+55 85 99999-8888', '+55 (85) 99999-8888'],
  ])('%s → %s', (input, expected) => {
    expect(formatPhone(input)).toBe(expected)
  })

  it('texto sem dígitos é devolvido como veio', () => {
    expect(formatPhone('desconhecido')).toBe('desconhecido')
  })
})

describe('displayName / initials', () => {
  it('usa o nome do contato quando existe', () => {
    expect(displayName({ contactName: 'Maria Souza', phoneNumber: '5585999998888' })).toBe(
      'Maria Souza'
    )
    expect(initials({ contactName: 'Maria Souza Lima', phoneNumber: '5585999998888' })).toBe('ML')
    expect(initials({ contactName: 'Maria', phoneNumber: '5585999998888' })).toBe('M')
  })

  it('sem nome, mostra o telefone formatado', () => {
    expect(displayName({ contactName: null, phoneNumber: '5585999998888' })).toBe(
      '+55 (85) 99999-8888'
    )
    expect(displayName({ contactName: '   ', phoneNumber: '5585999998888' })).toBe(
      '+55 (85) 99999-8888'
    )
    expect(initials({ contactName: null, phoneNumber: '5585999998888' })).toBe('88')
  })
})
