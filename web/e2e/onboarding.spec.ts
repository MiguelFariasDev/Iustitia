import { expect, test } from '@playwright/test'
import { GUEST_STATE } from './helpers'

test.describe('Cadastro (/cadastro) — duas etapas', () => {
  test.use({ storageState: GUEST_STATE })

  test.beforeEach(async ({ page }) => {
    await page.goto('/cadastro')
    await expect(page.getByRole('heading', { name: 'Crie sua conta' })).toBeVisible()
  })

  test('mostra o progresso 01 Você / 02 Escritório', async ({ page }) => {
    await expect(page.getByText('01 Você')).toHaveAttribute('aria-current', 'step')
  })

  test('valida a primeira etapa antes de avançar', async ({ page }) => {
    await page.getByRole('button', { name: 'Continuar' }).click()
    await expect(page.getByText('Informe o nome completo.')).toBeVisible()
  })

  test('aceita frase-senha e avança, preservando os dados ao voltar', async ({ page }) => {
    await page.getByLabel('Nome completo').fill('Ana Beatriz Ferraz')
    await page.getByLabel('E-mail profissional').fill('ana@ferraz.adv.br')
    await page.getByLabel('Senha', { exact: true }).fill('cavalo bateria grampo certo')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await expect(page.getByRole('heading', { name: 'Seu escritório' })).toBeVisible()
    await page.getByRole('button', { name: 'Voltar' }).click()
    await expect(page.getByLabel('Nome completo')).toHaveValue('Ana Beatriz Ferraz')
  })
})

test.describe('Onboarding (/onboarding) — exige sessão', () => {
  test.use({ storageState: GUEST_STATE })

  test('visitante é mandado ao login, preservando o destino', async ({ page }) => {
    await page.goto('/onboarding')
    await expect(page).toHaveURL(/\/login\?next=%2Fonboarding/)
  })
})

test.describe('Landing (/)', () => {
  test.use({ storageState: GUEST_STATE })

  test('visitante vê a landing com um único h1 e a chamada principal', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1)
    await expect(page.getByRole('link', { name: /Começar grátis/ }).first()).toHaveAttribute('href', '/cadastro')
  })

  test('360px sem rolagem horizontal', async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 800 })
    await page.goto('/')
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow).toBeLessThanOrEqual(0)
  })
})
