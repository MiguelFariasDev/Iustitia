import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

/**
 * Roda contra a API simulada (NEXT_PUBLIC_API_MOCKS=true): 27 publicações, 20 por página,
 * mesmas regras da API (filtros, ordenação, transições de status).
 */
test.describe('Publicações', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/publicacoes')
    await waitForPageReady(page, 'Publicações')
    await expect(page.getByRole('table')).toBeVisible()
  })

  test('mostra os cartões de resumo e a primeira página', async ({ page }) => {
    const resumo = page.getByRole('region', { name: 'Resumo das publicações' })
    await expect(resumo.getByText('Não tratadas de hoje')).toBeVisible()
    await expect(resumo.getByText('Tratadas esta semana')).toBeVisible()

    // cabeçalho + 20 linhas
    await expect(page.getByRole('row')).toHaveCount(21)
    await expect(page.getByText('Página 1 de 2')).toBeVisible()
  })

  test('filtra por status e reflete o filtro na URL', async ({ page }) => {
    await page.getByRole('button', { name: 'Arquivada' }).click()

    await expect(page).toHaveURL(/status=archived/)
    await expect(page.getByText('Nenhuma publicação encontrada')).toBeVisible()
  })

  test('um filtro vindo da URL (cartões do painel) abre a lista já filtrada', async ({ page }) => {
    await page.goto('/publicacoes?isRead=false')

    await expect(page.getByRole('button', { name: 'Não lidas' })).toHaveAttribute(
      'aria-pressed',
      'true'
    )
    await expect(page.getByRole('row')).toHaveCount(19) // 18 não lidas + cabeçalho
  })

  test('busca pelo processo e pelo cliente', async ({ page }) => {
    const busca = page.getByRole('searchbox', { name: 'Pesquisar publicações' })

    await busca.fill('horizonte')
    await expect(page).toHaveURL(/busca=horizonte/)
    await expect(
      page.getByRole('cell', { name: 'Transportadora Horizonte Ltda.' }).first()
    ).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Maria Aparecida Silva' })).toHaveCount(0)

    await busca.fill('zzz-inexistente')
    await expect(page.getByText('Nenhuma publicação encontrada')).toBeVisible()
    await page
      .getByRole('button', { name: /Limpar filtros/ })
      .first()
      .click()
    await expect(page.getByRole('row')).toHaveCount(21)
  })

  test('pagina e ordena pelo cabeçalho', async ({ page }) => {
    await page.getByRole('button', { name: 'Próxima página' }).click()
    await expect(page).toHaveURL(/page=2/)
    await expect(page.getByText('Página 2 de 2')).toBeVisible()
    await expect(page.getByRole('row')).toHaveCount(8) // 7 + cabeçalho

    await page.getByRole('button', { name: /divulgado em/i }).click()
    await expect(page).toHaveURL(/ordem=oldest/)
    await expect(page.getByRole('columnheader', { name: /divulgado em/i })).toHaveAttribute(
      'aria-sort',
      'ascending'
    )
  })

  test('seleciona publicações e marca como tratadas em massa', async ({ page }) => {
    await page.getByRole('button', { name: 'Não tratada' }).click()
    await expect(page).toHaveURL(/status=untreated/)

    const linhas = page.getByRole('checkbox', { name: /^Selecionar publicação do processo/ })
    await linhas.nth(0).check()
    await linhas.nth(1).check()
    await expect(page.getByText('2 selecionada(s)')).toBeVisible()

    await page.getByRole('button', { name: 'Marcar como tratada' }).click()

    await expect(page.getByText('2 publicação(ões) marcada(s) como tratada(s).')).toBeVisible()
    await expect(page.getByText('2 selecionada(s)')).toBeHidden()

    await page.getByRole('button', { name: 'Tratada', exact: true }).click()
    await expect(page.getByRole('row')).toHaveCount(3) // 2 tratadas + cabeçalho
  })

  test('exporta o CSV com todas as publicações do filtro', async ({ page }) => {
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Exportar CSV' }).click()

    expect((await download).suggestedFilename()).toMatch(/^publicacoes-\d{4}-\d{2}-\d{2}\.csv$/)
  })

  test('abre o detalhe, lê o texto integral e trata a publicação', async ({ page }) => {
    await page
      .getByRole('link', { name: /^5001000/ })
      .first()
      .click()

    await expect(page).toHaveURL(/\/publicacoes\/pub-/)
    await expect(page.getByRole('heading', { name: 'Publicação', level: 1 })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Dados da publicação' })).toBeVisible()
    await expect(page.getByRole('region', { name: 'Texto da publicação' })).toBeVisible()

    await page.getByRole('tab', { name: 'Metadados' }).click()
    await expect(page.getByText('classe', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Marcar como tratada' }).click()
    await expect(page.getByText('Publicação marcada como tratada.')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Marcar como tratada' })).toBeDisabled()

    await page.getByRole('link', { name: 'Voltar para publicações' }).click()
    await expect(page).toHaveURL(/\/publicacoes$/)
  })

  test('o link do DJEN abre em nova aba com rel seguro', async ({ page }) => {
    await page
      .getByRole('link', { name: /^5001000/ })
      .first()
      .click()

    const link = page.getByRole('link', { name: /Acessar no DJEN/ })
    await expect(link).toHaveAttribute('target', '_blank')
    await expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  })
})
