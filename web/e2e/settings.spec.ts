import { expect, test } from '@playwright/test'
import { dismissOnboardingHints } from './helpers'

test.describe('Configurações', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
  })

  test('navega entre as seções de configuração', async ({ page }) => {
    await page.goto('/configuracoes/perfil')
    await expect(page.getByRole('heading', { name: 'Configurações', level: 1 })).toBeVisible()

    for (const [label, url] of [
      ['Escritório', /\/configuracoes\/escritorio$/],
      ['Usuários', /\/configuracoes\/usuarios$/],
      ['Notificações', /\/configuracoes\/notificacoes$/],
      ['Integrações', /\/configuracoes\/integracoes$/],
      ['Auditoria', /\/configuracoes\/auditoria$/],
    ] as const) {
      await page.getByRole('link', { name: new RegExp(`^${label}`) }).click()
      await expect(page).toHaveURL(url)
    }
  })

  test('mostra os dados do perfil', async ({ page }) => {
    await page.goto('/configuracoes/perfil')
    await expect(page.getByRole('heading', { name: 'Dados pessoais' })).toBeVisible()
  })

  test('valida campos obrigatórios do escritório', async ({ page }) => {
    await page.goto('/configuracoes/escritorio')
    await expect(page.getByRole('heading', { name: 'Dados do escritório' })).toBeVisible()

    const salvar = page.getByRole('button', { name: 'Salvar alterações' })
    // Só habilita depois de alguma alteração (formulário "sujo").
    await expect(salvar).toBeDisabled()

    await page.getByLabel('Nome do escritório').fill('')
    await expect(salvar).toBeEnabled()

    await salvar.click()
    await expect(page.getByLabel('Nome do escritório')).toHaveAttribute('aria-invalid', 'true')
  })

  test('lista os usuários do escritório', async ({ page }) => {
    await page.goto('/configuracoes/usuarios')
    await expect(page.getByRole('table')).toBeVisible()
    await expect(page.getByRole('button', { name: /Convidar/ })).toBeVisible()
  })

  test('alterna uma preferência de notificação', async ({ page }) => {
    await page.goto('/configuracoes/notificacoes')
    await expect(page.getByRole('heading', { name: 'Preferências de notificação' })).toBeVisible()

    const salvar = page.getByRole('button', { name: 'Salvar preferências' })
    await expect(salvar).toBeDisabled()

    // O primeiro switch habilitado: os obrigatórios (alertas de prazo no app) são travados.
    await page.locator('button[role="switch"]:not([disabled])').first().click()
    await expect(salvar).toBeEnabled()
  })

  test('lista as integrações disponíveis', async ({ page }) => {
    await page.goto('/configuracoes/integracoes')
    await expect(page.getByRole('heading', { level: 2 }).first()).toBeVisible()
  })

  test('mostra o registro de auditoria', async ({ page }) => {
    await page.goto('/configuracoes/auditoria')
    await expect(page.getByRole('table')).toBeVisible()
  })

  test('alterna o tema pelo submenu do usuário', async ({ page }) => {
    await page.goto('/painel')
    const html = page.locator('html')
    await expect(html).not.toHaveClass(/dark/)

    await page.getByRole('button', { name: /^Menu do usuário:/ }).click()
    await page.getByRole('menuitem', { name: 'Tema' }).click()
    await page.getByRole('menuitemradio', { name: 'Escuro' }).click()

    await expect(html).toHaveClass(/dark/)
  })
})
