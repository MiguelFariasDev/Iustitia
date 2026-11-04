import { expect, type Page } from '@playwright/test'

/** Credenciais do modo de autenticação simulada (NEXT_PUBLIC_AUTH_MODE=mock). */
export const CREDENTIALS = {
  email: 'ana.souza@escritorio.com.br',
  password: 'demo1234',
} as const

/**
 * Preenche e envia o formulário de login. `exact` é obrigatório em "Senha": o botão
 * "Mostrar senha" também casa com a busca por rótulo parcial.
 */
export async function signIn(page: Page, password: string = CREDENTIALS.password) {
  await page.getByLabel('E-mail', { exact: true }).fill(CREDENTIALS.email)
  await page.getByLabel('Senha', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Entrar' }).click()
}

/**
 * Escolhe uma opção em um Select do Radix (um `combobox`, não um `<select>` nativo:
 * `selectOption` não funciona nele).
 */
export async function chooseOption(page: Page, label: string | RegExp, option: string) {
  await page.getByLabel(label).click()
  await page.getByRole('option', { name: option, exact: true }).click()
}

/** Sessão vazia, para specs que precisam começar deslogados. */
export const GUEST_STATE = { cookies: [], origins: [] }

/**
 * Dispensa os avisos de primeiro acesso antes do app carregar, para que não cubram
 * elementos nos testes. Chame antes do `page.goto`.
 */
export async function dismissOnboardingHints(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem(
      'adv-ui',
      JSON.stringify({
        state: {
          theme: 'system',
          sidebarCollapsed: false,
          dismissedHints: ['publications-intake', 'alerts-classification', 'agenda-week'],
        },
        version: 1,
      })
    )
  })
}

/** Espera o fim dos skeletons de carregamento de uma página de dados. */
export async function waitForPageReady(page: Page, headingName: string | RegExp) {
  await expect(page.getByRole('heading', { name: headingName, level: 1 })).toBeVisible()
  await expect(page.locator('[role="status"][aria-label^="Carregando"]')).toHaveCount(0)
}
