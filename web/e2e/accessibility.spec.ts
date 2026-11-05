import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'
import { dismissOnboardingHints, GUEST_STATE } from './helpers'

/** Regras WCAG 2.1 níveis A e AA — o compromisso declarado do produto. */
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']

async function analyze(page: Page) {
  // Os botões do app têm `transition-colors` (150 ms). Medido no meio da transição — logo após
  // um botão sair de "desabilitado" ao fim de uma consulta —, o contraste sai falso-negativo.
  // Esperar o assentamento mede o estado final, que é o que a pessoa vê.
  await page.waitForTimeout(300)

  return new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze()
}

const authenticatedPages = [
  ['/painel', 'Painel de gestão'],
  ['/publicacoes', 'Publicações'],
  ['/alertas', 'Alertas'],
  ['/publicacoes/pub-002', 'Publicação'],
  ['/alertas/alert-001', 'Alerta'],
  ['/agenda', 'Agenda'],
  ['/processos', 'Processos'],
  ['/contatos', 'Contatos'],
  ['/contatos/novo', 'Novo contato'],
  ['/documentos', 'Documentos'],
  ['/indicadores', 'Indicadores'],
  ['/financeiro', 'Financeiro'],
  ['/configuracoes/perfil', 'Configurações'],
  ['/configuracoes/usuarios', 'Configurações'],
  ['/configuracoes/integracoes', 'Configurações'],
] as const

test.describe('Acessibilidade — rotas autenticadas', () => {
  for (const [path, heading] of authenticatedPages) {
    test(`${path} não tem violações WCAG 2.1 AA`, async ({ page }) => {
      await dismissOnboardingHints(page)
      await page.goto(path)
      await expect(page.getByRole('heading', { name: heading, level: 1 })).toBeVisible()
      await expect(page.locator('[role="status"][aria-label^="Carregando"]')).toHaveCount(0)

      const { violations } = await analyze(page)
      expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
    })
  }

  test('o sino de notificações aberto não tem violações WCAG 2.1 AA', async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/painel')
    await page.getByRole('button', { name: /^Notificações/ }).click()
    await expect(page.getByRole('list', { name: 'Últimos alertas' })).toBeVisible()

    const { violations } = await analyze(page)
    expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
  })

  test('a lista de publicações é operável só pelo teclado (filtro, seleção e ação em massa)', async ({
    page,
  }) => {
    await dismissOnboardingHints(page)
    await page.goto('/publicacoes')
    await expect(page.getByRole('table')).toBeVisible()

    const chip = page.getByRole('button', { name: 'Não tratada' })
    await chip.focus()
    await page.keyboard.press('Enter')
    await expect(chip).toHaveAttribute('aria-pressed', 'true')

    const primeira = page
      .getByRole('checkbox', { name: /^Selecionar publicação do processo/ })
      .first()
    await primeira.focus()
    await page.keyboard.press('Space')
    await expect(page.getByText('1 selecionada(s)')).toBeVisible()
    // O foco NÃO se perde ao marcar (a tabela não pode remontar a cada seleção).
    await expect(primeira).toBeFocused()

    await page.getByRole('button', { name: 'Marcar como lida' }).focus()
    await page.keyboard.press('Enter')
    await expect(page.getByText('1 publicação(ões) marcada(s) como lida(s).')).toBeVisible()
  })

  test('o diálogo de upload de documentos não tem violações WCAG 2.1 AA', async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/documentos')
    await page.getByRole('button', { name: 'Upload' }).click()
    await expect(page.getByRole('dialog', { name: 'Enviar documento' })).toBeVisible()

    const { violations } = await analyze(page)
    expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
  })

  test('o diálogo de novo lançamento não tem violações WCAG 2.1 AA', async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/financeiro')
    await page.getByRole('button', { name: 'Novo lançamento' }).click()
    await expect(page.getByRole('dialog', { name: 'Novo lançamento' })).toBeVisible()

    const { violations } = await analyze(page)
    expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
  })

  test('as telas de gráficos (indicadores e fluxo de caixa) são acessíveis com os gráficos montados', async ({
    page,
  }) => {
    await dismissOnboardingHints(page)
    await page.goto('/indicadores')
    await expect(page.locator('.recharts-wrapper')).toHaveCount(3)
    let { violations } = await analyze(page)
    expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])

    await page.goto('/financeiro')
    await page.getByRole('tab', { name: 'Fluxo de caixa' }).click()
    await expect(page.locator('.recharts-wrapper')).toHaveCount(1)
    ;({ violations } = await analyze(page))
    expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
  })

  test('o tema escuro mantém o contraste exigido', async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.emulateMedia({ colorScheme: 'dark' })
    await page.goto('/painel')
    await expect(page.getByRole('heading', { name: 'Painel de gestão', level: 1 })).toBeVisible()

    const { violations } = await analyze(page)
    expect(violations.map((v) => v.id)).toEqual([])
  })

  test('a navegação principal é percorrível por teclado', async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/painel')

    // Primeiro Tab: o skip link, que leva ao conteúdo principal.
    await page.keyboard.press('Tab')
    const focado = page.locator(':focus')
    await expect(focado).toBeVisible()
    await expect(focado).toHaveText(/conteúdo/i)
  })
})

test.describe('Acessibilidade — rotas públicas', () => {
  test.use({ storageState: GUEST_STATE })

  for (const path of ['/', '/login', '/esqueci-senha', '/cadastro', '/termos'] as const) {
    test(`${path} não tem violações WCAG 2.1 AA`, async ({ page }) => {
      await page.goto(path)
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

      const { violations } = await analyze(page)
      expect(violations.map((v) => `${v.id}: ${v.nodes.length} nó(s)`)).toEqual([])
    })
  }

  test('a página 404 é acessível', async ({ page }) => {
    await page.goto('/rota-que-nao-existe')
    const { violations } = await analyze(page)
    expect(violations.map((v) => v.id)).toEqual([])
  })
})
