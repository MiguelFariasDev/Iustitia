import { expect, test } from '@playwright/test'
import { chooseOption, GUEST_STATE } from './helpers'

test.use({ storageState: GUEST_STATE })

test.describe('Cadastro de escritório (onboarding)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/onboarding')
    await expect(page.getByRole('heading', { name: 'Cadastre seu escritório' })).toBeVisible()
  })

  test('mostra o progresso das três etapas', async ({ page }) => {
    await expect(page.getByText('Etapa 1 de 3: Escritório')).toBeVisible()
  })

  test('valida a primeira etapa antes de avançar', async ({ page }) => {
    await page.getByRole('button', { name: 'Continuar' }).click()

    await expect(page.getByText('Informe o nome do escritório.')).toBeVisible()
    await expect(page.getByText('Etapa 1 de 3: Escritório')).toBeVisible()
  })

  test('valida a senha e a confirmação na segunda etapa', async ({ page }) => {
    await page.getByLabel('Nome do escritório').fill('Ferraz & Associados')
    await chooseOption(page, 'Seccional da OAB', 'OAB/RS')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await expect(page.getByText('Etapa 2 de 3: Responsável')).toBeVisible()

    await page.getByLabel('Seu nome completo').fill('Ana Beatriz Ferraz')
    await page.getByLabel('E-mail', { exact: true }).fill('ana@ferraz.adv.br')
    await page.getByLabel('Senha', { exact: true }).fill('SenhaForte123')
    await page.getByLabel('Confirmar senha').fill('OutraSenha123')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await expect(page.getByText('As senhas não conferem.')).toBeVisible()
  })

  test('permite voltar para a etapa anterior sem perder os dados', async ({ page }) => {
    await page.getByLabel('Nome do escritório').fill('Ferraz & Associados')
    await chooseOption(page, 'Seccional da OAB', 'OAB/RS')
    await page.getByRole('button', { name: 'Continuar' }).click()
    await expect(page.getByText('Etapa 2 de 3: Responsável')).toBeVisible()

    await page.getByRole('button', { name: 'Voltar' }).click()

    await expect(page.getByText('Etapa 1 de 3: Escritório')).toBeVisible()
    await expect(page.getByLabel('Nome do escritório')).toHaveValue('Ferraz & Associados')
  })

  test('completa o cadastro e chega ao painel', async ({ page }) => {
    await page.getByLabel('Nome do escritório').fill('Ferraz & Associados')
    await chooseOption(page, 'Seccional da OAB', 'OAB/RS')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await page.getByLabel('Seu nome completo').fill('Ana Beatriz Ferraz')
    await page.getByLabel('E-mail', { exact: true }).fill('ana@ferraz.adv.br')
    await page.getByLabel('Senha', { exact: true }).fill('SenhaForte123')
    await page.getByLabel('Confirmar senha').fill('SenhaForte123')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await expect(page.getByText('Etapa 3 de 3: Preferências')).toBeVisible()
    await chooseOption(page, 'Tamanho da equipe', '2 a 5 pessoas')
    await page.getByRole('checkbox', { name: /Cível/ }).first().check()
    await page.getByRole('checkbox', { name: /revisão humana/ }).check()
    await page.getByRole('checkbox', { name: /termos de uso/ }).check()

    await page.getByRole('button', { name: 'Criar escritório' }).click()

    await expect(page).toHaveURL(/\/painel$/, { timeout: 15_000 })
  })

  test('exige aceite dos termos antes de criar', async ({ page }) => {
    await page.getByLabel('Nome do escritório').fill('Ferraz & Associados')
    await chooseOption(page, 'Seccional da OAB', 'OAB/RS')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await page.getByLabel('Seu nome completo').fill('Ana Beatriz Ferraz')
    await page.getByLabel('E-mail', { exact: true }).fill('ana@ferraz.adv.br')
    await page.getByLabel('Senha', { exact: true }).fill('SenhaForte123')
    await page.getByLabel('Confirmar senha').fill('SenhaForte123')
    await page.getByRole('button', { name: 'Continuar' }).click()

    await chooseOption(page, 'Tamanho da equipe', 'Só eu')
    await page.getByRole('checkbox', { name: /Cível/ }).first().check()
    await page.getByRole('button', { name: 'Criar escritório' }).click()

    await expect(
      page.getByText('É necessário aceitar os termos de uso e a política de privacidade.')
    ).toBeVisible()
  })
})
