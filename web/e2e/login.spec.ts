import { expect, test } from '@playwright/test'
import { GUEST_STATE, signIn } from './helpers'

test.use({ storageState: GUEST_STATE })

test.describe('Login', () => {
  test('exige e-mail e senha antes de enviar', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: 'Entrar' }).click()

    await expect(page.getByText('Informe o e-mail.')).toBeVisible()
    await expect(page.getByText('Informe a senha.')).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })

  test('rejeita credenciais inválidas com mensagem em alerta', async ({ page }) => {
    await page.goto('/login')
    await signIn(page, 'senha-errada')

    // Filtra pelo texto: o route announcer do Next.js também tem role="alert".
    await expect(
      page.getByRole('alert').filter({ hasText: 'E-mail ou senha inválidos.' })
    ).toBeVisible()
    await expect(page).toHaveURL(/\/login$/)
  })

  test('autentica e chega ao painel', async ({ page }) => {
    await page.goto('/login')
    await signIn(page)

    await expect(page).toHaveURL(/\/painel$/)
    await expect(page.getByRole('heading', { name: 'Painel de gestão', level: 1 })).toBeVisible()
  })

  test('preserva o destino original em ?next=', async ({ page }) => {
    await page.goto('/publicacoes')
    await expect(page).toHaveURL(/\/login\?next=%2Fpublicacoes$/)

    await signIn(page)

    await expect(page).toHaveURL(/\/publicacoes$/)
  })

  test('ignora ?next= externo (open redirect)', async ({ page }) => {
    await page.goto('/login?next=https://exemplo-malicioso.test')
    await signIn(page)

    await expect(page).toHaveURL(/\/painel$/)
  })

  test('mostra e oculta a senha', async ({ page }) => {
    await page.goto('/login')
    const senha = page.getByLabel('Senha', { exact: true })
    await expect(senha).toHaveAttribute('type', 'password')

    await page.getByRole('button', { name: 'Mostrar senha' }).click()
    await expect(senha).toHaveAttribute('type', 'text')
  })

  test('redireciona rota protegida para o login', async ({ page }) => {
    await page.goto('/configuracoes/usuarios')
    await expect(page).toHaveURL(/\/login\?next=/)
  })

  test('envia o pedido de redefinição sem revelar se a conta existe', async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('link', { name: 'Esqueci minha senha' }).click()
    await expect(page).toHaveURL(/\/esqueci-senha$/)

    await page.getByLabel('E-mail', { exact: true }).fill('desconhecido@exemplo.com')
    await page.getByRole('button', { name: 'Enviar link' }).click()

    await expect(page.getByText(/Se existir uma conta para/)).toBeVisible()
  })
})
