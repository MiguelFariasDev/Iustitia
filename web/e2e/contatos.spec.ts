import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

/** API simulada: 5 contatos em memória (reiniciam a cada carregamento da página). */
test.describe('Contatos', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/contatos')
    await waitForPageReady(page, 'Contatos')
    await expect(page.getByRole('table')).toBeVisible()
  })

  test('lista os contatos e avisa que são dados de demonstração', async ({ page }) => {
    await expect(page.getByRole('note')).toContainText('Dados de demonstração')
    await expect(page.getByRole('row', { name: /Carlos Eduardo Pereira/ })).toBeVisible()
    await expect(page.getByRole('row', { name: /Papelaria Central LTDA/ })).toBeVisible()
    await expect(page.getByRole('row')).toHaveCount(1 + 5)
  })

  test('filtra por tipo pelos chips', async ({ page }) => {
    await page.getByRole('button', { name: 'Parte contrária' }).click()

    await expect(page.getByRole('row', { name: /Construtora Horizonte/ })).toBeVisible()
    await expect(page.getByRole('row', { name: /Helena Rocha Martins/ })).toBeHidden()
    await expect(page.getByRole('row')).toHaveCount(1 + 2)
  })

  test('busca por nome, documento ou e-mail', async ({ page }) => {
    await page.getByRole('searchbox').fill('papelaria')

    await expect(page.getByRole('row', { name: /Papelaria Central/ })).toBeVisible()
    await expect(page.getByRole('row')).toHaveCount(1 + 1)
  })

  test('cria um contato e o vê na lista', async ({ page }) => {
    await page.getByRole('link', { name: 'Novo contato' }).click()
    await expect(page).toHaveURL(/\/contatos\/novo$/)
    await waitForPageReady(page, 'Novo contato')

    await page.getByLabel('Nome').fill('Zélia Antunes')
    await page.getByLabel('CPF ou CNPJ').fill('11144477735')
    await expect(page.getByLabel('CPF ou CNPJ')).toHaveValue('111.444.777-35')
    await page.getByLabel('E-mail').fill('zelia@exemplo.com.br')
    await page.getByRole('button', { name: 'Salvar' }).click()

    await expect(page).toHaveURL(/\/contatos$/)
    await expect(page.getByRole('row', { name: /Zélia Antunes/ })).toBeVisible()
  })

  test('valida o formulário antes de enviar', async ({ page }) => {
    await page.getByRole('link', { name: 'Novo contato' }).click()
    await waitForPageReady(page, 'Novo contato')

    await page.getByRole('button', { name: 'Salvar' }).click()
    await expect(page.getByText('Informe o nome do contato.')).toBeVisible()

    await page.getByLabel('Nome').fill('Fulano')
    await page.getByLabel('CPF ou CNPJ').fill('11144477736')
    await page.getByRole('button', { name: 'Salvar' }).click()
    await expect(page.getByText(/Documento inválido/)).toBeVisible()
    await expect(page).toHaveURL(/\/contatos\/novo$/)
  })

  test('edita um contato', async ({ page }) => {
    await page.getByRole('link', { name: 'Editar Helena Rocha Martins' }).click()
    await waitForPageReady(page, 'Editar contato')
    await expect(page.getByLabel('Nome')).toHaveValue('Helena Rocha Martins')

    await page.getByLabel('Nome').fill('Helena Rocha Martins Souza')
    await page.getByRole('button', { name: 'Salvar alterações' }).click()

    await expect(page).toHaveURL(/\/contatos$/)
    await expect(page.getByRole('row', { name: /Helena Rocha Martins Souza/ })).toBeVisible()
  })

  test('exclui um contato só depois de confirmar', async ({ page }) => {
    await page.getByRole('button', { name: 'Excluir Dr. Roberto Almeida' }).click()
    const dialogo = page.getByRole('alertdialog')
    await dialogo.getByRole('button', { name: 'Cancelar' }).click()
    await expect(page.getByRole('row', { name: /Dr\. Roberto Almeida/ })).toBeVisible()

    await page.getByRole('button', { name: 'Excluir Dr. Roberto Almeida' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Excluir' }).click()

    await expect(page.getByRole('row', { name: /Dr\. Roberto Almeida/ })).toBeHidden()
  })
})
