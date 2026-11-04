import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

/** API simulada: 6 alertas — 4 não lidos (2 de publicação) e 2 já lidos. */
test.describe('Alertas', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/alertas')
    await waitForPageReady(page, 'Alertas')
    await expect(page.getByRole('list', { name: 'Importantes' })).toBeVisible()
  })

  test('abre na aba "Importantes" com as contagens de cada aba', async ({ page }) => {
    await expect(page.getByRole('tab', { name: 'Importantes (4)' })).toHaveAttribute(
      'aria-selected',
      'true'
    )
    await expect(page.getByRole('tab', { name: 'Todos (6)' })).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Publicações não tratadas (2)' })).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Outros (3)' })).toBeVisible()
  })

  test('mostra apenas os alertas não lidos na aba inicial', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Nova publicação: intimação' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Backup concluído' })).toBeHidden()
  })

  test('troca de aba pelo teclado', async ({ page }) => {
    await page.getByRole('tab', { name: /^Importantes/ }).focus()
    await page.keyboard.press('ArrowRight')

    await expect(page.getByRole('tab', { name: /^Todos/ })).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByRole('heading', { name: 'Backup concluído' })).toBeVisible()
  })

  test('filtra a aba de publicações', async ({ page }) => {
    await page.getByRole('tab', { name: /^Publicações não tratadas/ }).click()

    await expect(page.getByRole('heading', { name: 'Nova publicação: sentença' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Tarefa vence amanhã' })).toBeHidden()
  })

  test('descarta um alerta e atualiza as contagens', async ({ page }) => {
    await page.getByRole('button', { name: 'Descartar alerta: Tarefa vence amanhã' }).click()

    await expect(page.getByRole('heading', { name: 'Tarefa vence amanhã' })).toBeHidden()
    await expect(page.getByRole('tab', { name: 'Importantes (3)' })).toBeVisible()
    await expect(page.getByRole('tab', { name: 'Todos (5)' })).toBeVisible()
  })

  test('"Marcar todos como lidos" leva ao estado vazio', async ({ page }) => {
    await page.getByRole('button', { name: 'Marcar todos como lidos' }).click()

    await expect(page.getByText('Nenhum alerta importante no momento')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Marcar todos como lidos' })).toBeDisabled()
  })

  test('o alerta de publicação leva à publicação', async ({ page }) => {
    await page
      .getByRole('link', { name: /Ver publicação/ })
      .first()
      .click()

    await expect(page).toHaveURL(/\/publicacoes\/pub-/)
  })
})

test.describe('Sino de notificações', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/painel')
    await expect(page.getByRole('heading', { name: 'Painel de gestão', level: 1 })).toBeVisible()
  })

  test('mostra o contador, os últimos alertas e o atalho para todos', async ({ page }) => {
    const bell = page.getByRole('button', { name: 'Notificações (4 não lidas)' })
    await bell.click()

    await expect(
      page.getByRole('list', { name: 'Últimos alertas' }).getByRole('listitem')
    ).toHaveCount(5)
    await page
      .getByRole('dialog')
      .getByRole('link', { name: /Ver todos/ })
      .click()
    await expect(page).toHaveURL(/\/alertas$/)
  })

  test('marcar como lido atualiza o contador', async ({ page }) => {
    await page.getByRole('button', { name: 'Notificações (4 não lidas)' }).click()
    await page.getByRole('button', { name: /Marcar como lido: Nova publicação: intimação/ }).click()

    await expect(page.getByRole('button', { name: 'Notificações (3 não lidas)' })).toBeVisible()
  })
})
