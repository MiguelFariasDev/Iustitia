import { expect, test } from '@playwright/test'
import { dismissOnboardingHints } from './helpers'

/**
 * Notificação in-app. Com a API simulada não há Supabase, então o canal de Realtime é
 * substituído pelo evento de janela `adv:alert-created` (ver features/alertas/realtime.ts):
 * ele atravessa o MESMO tratamento do broadcast real — validação do payload, toast e
 * invalidação do cache. A assinatura do canal privado em si é coberta pelos testes unitários
 * do hook e pela migration 013 (policy de realtime.messages).
 */
async function pushAlert(page: import('@playwright/test').Page, detail: Record<string, unknown>) {
  await page.evaluate(
    (payload) => window.dispatchEvent(new CustomEvent('adv:alert-created', { detail: payload })),
    detail
  )
}

test.describe('Notificação in-app em tempo real', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/painel')
    await expect(page.getByRole('button', { name: 'Notificações (4 não lidas)' })).toBeVisible()
  })

  test('um alerta novo mostra o toast e atualiza o contador do sino', async ({ page }) => {
    await pushAlert(page, {
      alertId: 'e2e-alert-1',
      type: 'Publication',
      title: 'Nova publicação: despacho',
      description: 'Processo 5009999-99.2026.8.26.0001 — TJSP',
      referenceId: 'pub-009',
      createdAt: new Date().toISOString(),
    })

    await expect(page.getByText('Nova publicação: despacho').first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Notificações (5 não lidas)' })).toBeVisible()
  })

  test('o alerta recebido aparece no topo do sino e leva ao detalhe', async ({ page }) => {
    await pushAlert(page, {
      alertId: 'e2e-alert-2',
      type: 'Other',
      title: 'Aviso em tempo real',
      description: null,
      referenceId: null,
      createdAt: new Date().toISOString(),
    })

    await page.getByRole('button', { name: 'Notificações (5 não lidas)' }).click()
    const primeiro = page
      .getByRole('list', { name: 'Últimos alertas' })
      .getByRole('listitem')
      .first()
    await expect(primeiro).toContainText('Aviso em tempo real')

    await primeiro.getByRole('link').click()
    await expect(page).toHaveURL(/\/alertas\/e2e-alert-2$/)
    await expect(page.getByRole('heading', { name: 'Aviso em tempo real' })).toBeVisible()
  })

  test('payload inválido é ignorado: sem toast e sem mudar o contador', async ({ page }) => {
    await pushAlert(page, { qualquer: 'coisa' })

    await page.waitForTimeout(300)
    await expect(page.getByRole('button', { name: 'Notificações (4 não lidas)' })).toBeVisible()
  })
})
