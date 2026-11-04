import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

test.describe('Painel de gestão', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/painel')
    await waitForPageReady(page, 'Painel de gestão')
  })

  test('mostra os cartões de métricas', async ({ page }) => {
    // Escopo na seção de indicadores: "Publicações não lidas" também titula um card abaixo.
    const metrics = page.getByRole('region', { name: 'Indicadores' })
    for (const label of [
      'Processos ativos',
      'Publicações não lidas',
      'Aguardando revisão humana',
      'Alertas',
    ]) {
      await expect(metrics.getByText(label, { exact: true })).toBeVisible()
    }
  })

  test('mostra as listas de publicações e alertas não lidos, clicáveis', async ({ page }) => {
    await expect(
      page.getByRole('heading', { name: 'Publicações não lidas', level: 2 })
    ).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Alertas', level: 2 })).toBeVisible()

    await page.getByRole('list', { name: 'Alertas' }).getByRole('link').first().click()
    await expect(page).toHaveURL(/\/alertas\/alert-/)
  })

  test('o cartão de publicações não lidas abre a lista já filtrada', async ({ page }) => {
    await page.getByRole('link', { name: 'Publicações não lidas — ver a lista' }).click()

    await expect(page).toHaveURL(/\/publicacoes\?isRead=false$/)
  })

  test('a raiz redireciona para o painel', async ({ page }) => {
    await page.goto('/')
    await expect(page).toHaveURL(/\/painel$/)
  })

  test('navega pela barra lateral até publicações e alertas', async ({ page }) => {
    const nav = page.getByRole('navigation', { name: 'Navegação principal' })

    await nav.getByRole('link', { name: /^Publicações/ }).click()
    await expect(page).toHaveURL(/\/publicacoes$/)

    await nav.getByRole('link', { name: /^Alertas/ }).click()
    await expect(page).toHaveURL(/\/alertas$/)
  })

  test('marca o item ativo da navegação com aria-current', async ({ page }) => {
    const nav = page.getByRole('navigation', { name: 'Navegação principal' })
    await expect(nav.getByRole('link', { name: 'Área de trabalho' })).toHaveAttribute(
      'aria-current',
      'page'
    )
  })

  test('recolhe e expande a barra lateral', async ({ page }) => {
    const toggle = page.getByRole('button', { name: 'Recolher barra lateral' })
    await toggle.click()
    await expect(page.getByRole('button', { name: 'Expandir barra lateral' })).toBeVisible()
  })

  test('abre a paleta de comandos com Ctrl+K', async ({ page }) => {
    await page.keyboard.press('Control+k')
    await expect(page.getByRole('dialog')).toBeVisible()
    await page.keyboard.press('Escape')
    await expect(page.getByRole('dialog')).toBeHidden()
  })

  test('mostra as badges de pendências na navegação', async ({ page }) => {
    const nav = page.getByRole('navigation', { name: 'Navegação principal' })
    await expect(nav.getByRole('link', { name: /Publicações.*27 pendentes/s })).toBeVisible()
    await expect(nav.getByRole('link', { name: /Alertas.*4 pendentes/s })).toBeVisible()
  })

  test('módulos das próximas fases levam a um placeholder, não a um 404', async ({ page }) => {
    const nav = page.getByRole('navigation', { name: 'Navegação principal' })
    await nav.getByRole('link', { name: /^Agenda/ }).click()

    await expect(page).toHaveURL(/\/agenda$/)
    await expect(page.getByRole('heading', { name: 'Agenda', level: 1 })).toBeVisible()
  })
})
