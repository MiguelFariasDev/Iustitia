import { filterNavigation, mainNavigation, settingsNavigation } from './navigation'
import { canAccess, screenLabel, type AccessContext } from '@/lib/auth/access'

const visible = (context: AccessContext) =>
  filterNavigation(
    mainNavigation,
    (screen) => canAccess(screen, context),
    (screen, fallback) => screenLabel(screen, fallback, context)
  ).flatMap((group) => group.items.map((item) => item.label))

describe('navegação filtrada pela matriz de acesso', () => {
  it('titular de escritório vê todo o menu', () => {
    const todos = mainNavigation.flatMap((group) => group.items.map((item) => item.label))

    expect(visible({ role: 'Owner', accountType: 'Firm' })).toEqual(todos)
  })

  it('advogado(a) de escritório perde Financeiro e Indicadores', () => {
    const itens = visible({ role: 'Lawyer', accountType: 'Firm' })

    expect(itens).not.toContain('Financeiro')
    expect(itens).not.toContain('Indicadores')
    expect(itens).toContain('Processos')
  })

  it('perfil sem acesso ao jurídico fica sem o grupo "Gestão" inteiro quando nada sobra nele', () => {
    const grupos = filterNavigation(
      mainNavigation,
      (screen) => canAccess(screen, { role: 'Intern', accountType: 'Firm' }),
      (_screen, fallback) => fallback
    )

    expect(grupos.every((group) => group.items.length > 0)).toBe(true)
    expect(grupos.flatMap((g) => g.items.map((i) => i.label))).toEqual([
      'Área de trabalho',
      'Agenda',
      'Documentos',
    ])
  })

  it('todo item do menu e das configurações declara a tela da matriz', () => {
    const itens = [...mainNavigation.flatMap((g) => g.items), ...settingsNavigation]

    expect(itens.every((item) => Boolean(item.screen))).toBe(true)
  })
})

describe('configurações para conta Solo', () => {
  const solo: AccessContext = { role: 'Owner', accountType: 'Solo' }
  const itens = settingsNavigation
    .filter((item) => canAccess(item.screen, solo))
    .map((item) => screenLabel(item.screen, item.label, solo))

  it('some Usuários e renomeia Escritório', () => {
    expect(itens).not.toContain('Usuários')
    expect(itens).toContain('Dados profissionais')
    expect(itens).not.toContain('Escritório')
  })
})
