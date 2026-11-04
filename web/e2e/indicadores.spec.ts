import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

test.describe('Indicadores', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/indicadores')
    await waitForPageReady(page, 'Indicadores')
    await expect(page.getByRole('region', { name: 'Indicadores principais' })).toBeVisible()
  })

  test('mostra os KPIs e o aviso de demonstração', async ({ page }) => {
    await expect(page.getByRole('note')).toContainText('Dados de demonstração')
    const kpis = page.getByRole('region', { name: 'Indicadores principais' })

    await expect(kpis.getByText('Total de processos ativos')).toBeVisible()
    await expect(kpis.getByText('48', { exact: true })).toBeVisible()
    await expect(kpis.getByText('Prazos vencendo')).toBeVisible()
    await expect(kpis.getByText('nos próximos 7 dias')).toBeVisible()
  })

  test('renderiza os três gráficos, cada um com descrição textual', async ({ page }) => {
    const rosca = page.getByRole('img', {
      name: /^Gráfico de rosca dos processos por área: Cível 31/,
    })
    const barras = page.getByRole('img', {
      name: /^Gráfico de barras dos processos por status: Ativo 48/,
    })
    const linha = page.getByRole('img', { name: /^Gráfico de linha da evolução de publicações/ })

    for (const grafico of [rosca, barras, linha]) {
      await expect(grafico).toBeVisible()
      // O Recharts desenha um <svg>: confirma que o gráfico de fato montou, não só o rótulo.
      await expect(grafico.locator('.recharts-wrapper')).toBeVisible()
    }
  })

  test('o filtro de período muda tarefas concluídas e a evolução, não a carteira', async ({
    page,
  }) => {
    const kpis = page.getByRole('region', { name: 'Indicadores principais' })
    const mes = page.getByRole('button', { name: 'Mês atual' })
    await expect(mes).toHaveAttribute('aria-pressed', 'true')
    await expect(kpis.getByText('31', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Ano' }).click()

    await expect(page.getByRole('button', { name: 'Ano' })).toHaveAttribute('aria-pressed', 'true')
    await expect(kpis.getByText('372', { exact: true })).toBeVisible()
    await expect(kpis.getByText('48', { exact: true })).toBeVisible()
  })

  test('mostra a tabela de produtividade por advogado', async ({ page }) => {
    const tabela = page.getByRole('table', { name: 'Produtividade por advogado' })

    await expect(tabela.getByRole('row', { name: /Ana Souza/ })).toBeVisible()
    await expect(tabela.getByRole('columnheader', { name: /Tarefas concluídas/ })).toBeVisible()
    await expect(tabela.getByRole('row')).toHaveCount(1 + 3)
  })

  test('o cartão de processos ativos leva à lista filtrada', async ({ page }) => {
    await page.getByRole('link', { name: /Processos ativos — ver a lista/ }).click()

    await expect(page).toHaveURL(/\/processos\?status=Ativo$/)
  })
})
