import { expect, test, type Page } from '@playwright/test'
import { chooseOption, dismissOnboardingHints, waitForPageReady } from './helpers'

/** API simulada: 7 lançamentos em memória — 3 honorários, 2 contas a pagar, 2 a receber. */
test.describe('Financeiro', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/financeiro')
    await waitForPageReady(page, 'Financeiro')
    await expect(page.getByRole('region', { name: 'Resumo financeiro' })).toBeVisible()
  })

  const resumo = (page: Page) => page.getByRole('region', { name: 'Resumo financeiro' })

  test('mostra os quatro totais e o aviso de demonstração', async ({ page }) => {
    await expect(page.getByRole('note')).toContainText('Dados de demonstração')

    await expect(resumo(page).getByText('Faturamento do mês')).toBeVisible()
    await expect(resumo(page).getByText(/R\$\s*11\.700,00/)).toBeVisible() // honorários a receber
    await expect(resumo(page).getByText(/R\$\s*3\.220,00/)).toBeVisible() // contas a pagar
    await expect(resumo(page).getByText(/R\$\s*3\.500,00/)).toBeVisible() // saldo
  })

  test('navega entre as abas, que mostram colunas diferentes', async ({ page }) => {
    await expect(page.getByRole('tab', { name: 'Honorários' })).toHaveAttribute(
      'aria-selected',
      'true'
    )
    await expect(page.getByRole('columnheader', { name: 'Processo' })).toBeVisible()

    await page.getByRole('tab', { name: 'Contas a pagar' }).click()
    await expect(page.getByRole('columnheader', { name: 'Fornecedor' })).toBeVisible()
    await expect(page.getByText('Aluguel do escritório')).toBeVisible()

    await page.getByRole('tab', { name: 'Contas a receber' }).click()
    await expect(page.getByText('Reembolso de despesas')).toBeVisible()

    await page.getByRole('tab', { name: 'Fluxo de caixa' }).click()
    const grafico = page.getByRole('img', { name: /Gráfico de barras de entradas e saídas/ })
    await expect(grafico.locator('.recharts-wrapper')).toBeVisible()
  })

  test('troca de aba pelo teclado', async ({ page }) => {
    await page.getByRole('tab', { name: 'Honorários' }).focus()
    await page.keyboard.press('ArrowRight')

    await expect(page.getByRole('tab', { name: 'Contas a pagar' })).toHaveAttribute(
      'aria-selected',
      'true'
    )
  })

  test('filtra por status e por período de vencimento', async ({ page }) => {
    await page.getByRole('button', { name: 'Atrasado' }).click()
    await expect(page.getByRole('row')).toHaveCount(1 + 1) // só o honorário atrasado
    await expect(page.getByRole('row', { name: /José Lima/ })).toBeVisible()
    await page.getByRole('button', { name: 'Atrasado' }).click()

    await chooseOption(page, 'Período de vencimento', 'Vence nos próximos 30 dias')
    await expect(page.getByRole('row', { name: /José Lima/ })).toBeHidden()
    await expect(page.getByRole('row', { name: /Maria Souza/ })).toBeVisible()
  })

  test('registra um lançamento e os totais acompanham', async ({ page }) => {
    await page.getByRole('button', { name: 'Novo lançamento' }).click()
    const dialogo = page.getByRole('dialog', { name: 'Novo lançamento' })

    await dialogo.getByRole('button', { name: 'Registrar' }).click()
    await expect(dialogo.getByText('Informe a descrição.')).toBeVisible()
    await expect(dialogo.getByText('O valor deve ser maior que zero.')).toBeVisible()

    await dialogo.getByLabel('Descrição').fill('Parecer jurídico')
    await dialogo.getByLabel('Cliente', { exact: true }).fill('Construtora Horizonte')
    await dialogo.getByLabel('Valor').pressSequentially('150000')
    await expect(dialogo.getByLabel('Valor')).toHaveValue('R$ 1.500,00')
    await dialogo.getByLabel('Vencimento').fill('2026-12-20')
    await dialogo.getByRole('button', { name: 'Registrar' }).click()

    await expect(dialogo).toBeHidden()
    await page.getByRole('tab', { name: 'Honorários' }).click()
    await expect(page.getByRole('row', { name: /Construtora Horizonte/ })).toBeVisible()
    // Honorários a receber: 11.700 + 1.500.
    await expect(resumo(page).getByText(/R\$\s*13\.200,00/)).toBeVisible()
  })

  test('exporta a aba atual em CSV; PDF avisa que ainda não existe', async ({ page }) => {
    await page.getByRole('button', { name: 'Exportar' }).click()
    const download = page.waitForEvent('download')
    await page.getByRole('menuitem', { name: 'Exportar em CSV' }).click()
    expect((await download).suggestedFilename()).toBe('financeiro-honorarios.csv')

    await page.getByRole('button', { name: 'Exportar' }).click()
    await page.getByRole('menuitem', { name: /Exportar em PDF/ }).click()
    await expect(page.getByText('Exportação em PDF ainda não disponível.')).toBeVisible()
  })
})
