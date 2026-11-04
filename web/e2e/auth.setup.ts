import { expect, test as setup } from '@playwright/test'
import { STORAGE_STATE } from '../playwright.config'
import { signIn } from './helpers'

/**
 * Faz login uma única vez pela interface e guarda a sessão em disco. Os demais specs
 * reaproveitam esse estado, em vez de repetir o formulário a cada teste.
 */
setup('autentica e salva a sessão', async ({ page }) => {
  await page.goto('/login')
  await signIn(page)

  await expect(page).toHaveURL(/\/painel$/)
  await expect(page.getByRole('heading', { name: 'Painel de gestão', level: 1 })).toBeVisible()

  await page.context().storageState({ path: STORAGE_STATE })
})
