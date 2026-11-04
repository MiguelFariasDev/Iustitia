import { readFileSync } from 'node:fs'
import { expect, test, type Page } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

/** API simulada: 6 documentos em memória. */
test.describe('Documentos', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/documentos')
    await waitForPageReady(page, 'Documentos')
    await expect(page.getByRole('list', { name: 'Documentos do escritório' })).toBeVisible()
  })

  const cards = (page: Page) =>
    page.getByRole('list', { name: 'Documentos do escritório' }).getByRole('listitem')

  test('lista os documentos mais recentes primeiro', async ({ page }) => {
    await expect(page.getByRole('note')).toContainText('Dados de demonstração')
    await expect(cards(page)).toHaveCount(6)
    await expect(cards(page).first()).toContainText('Petição inicial.pdf')
  })

  test('filtra por tipo de arquivo e por vínculo', async ({ page }) => {
    await page.getByRole('button', { name: 'Imagem' }).click()
    await expect(cards(page)).toHaveCount(2)

    await page.getByRole('button', { name: 'Imagem' }).click()
    await page.getByRole('button', { name: 'Avulso' }).click()
    await expect(cards(page)).toHaveCount(2)
    await expect(cards(page).first()).toContainText('Comprovante de pagamento.png')
  })

  test('busca pelo nome do arquivo e mostra o estado vazio', async ({ page }) => {
    await page.getByRole('searchbox').fill('procuração')
    await expect(cards(page)).toHaveCount(1)

    await page.getByRole('searchbox').fill('inexistente-xyz')
    await expect(page.getByRole('heading', { name: 'Nenhum documento encontrado' })).toBeVisible()
  })

  test('envia um documento e o vê no topo da lista', async ({ page }) => {
    await page.getByRole('button', { name: 'Upload' }).click()
    const dialogo = page.getByRole('dialog', { name: 'Enviar documento' })

    await dialogo.getByLabel('Arquivo').setInputFiles({
      name: 'novo-contrato.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('conteudo de teste'),
    })
    await dialogo.getByRole('button', { name: 'Enviar' }).click()

    await expect(dialogo).toBeHidden()
    await expect(cards(page)).toHaveCount(7)
    await expect(cards(page).first()).toContainText('novo-contrato.pdf')
  })

  test('recusa formato não aceito e exige a referência do vínculo', async ({ page }) => {
    await page.getByRole('button', { name: 'Upload' }).click()
    const dialogo = page.getByRole('dialog', { name: 'Enviar documento' })

    await dialogo.getByRole('button', { name: 'Enviar' }).click()
    await expect(dialogo.getByText('Selecione um arquivo.')).toBeVisible()

    await dialogo.getByLabel('Arquivo').setInputFiles({
      name: 'programa.exe',
      mimeType: 'application/octet-stream',
      buffer: Buffer.from('x'),
    })
    await dialogo.getByRole('button', { name: 'Enviar' }).click()
    await expect(dialogo.getByText(/Formato não aceito/)).toBeVisible()

    await dialogo.getByLabel('Arquivo').setInputFiles({
      name: 'ok.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('x'),
    })
    await dialogo.getByLabel('Vínculo').click()
    await page.getByRole('option', { name: 'Processo' }).click()
    await dialogo.getByRole('button', { name: 'Enviar' }).click()
    await expect(dialogo.getByText('Informe o número do processo.')).toBeVisible()
  })

  test('exclui um documento só depois de confirmar', async ({ page }) => {
    await page.getByRole('button', { name: 'Excluir Contrato de honorários.docx' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Excluir' }).click()

    await expect(cards(page)).toHaveCount(5)
    await expect(page.getByText('Contrato de honorários.docx')).toBeHidden()
  })

  test('baixar um documento de exemplo entrega um aviso, não um arquivo falso', async ({
    page,
  }) => {
    // Nome ASCII de propósito: o Chromium em Linux com locale POSIX troca nomes acentuados
    // por "download", e o teste não deve depender do locale de quem o roda.
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Baixar Foto do local do acidente.jpg' }).click()
    const arquivo = await download

    expect(arquivo.suggestedFilename()).toBe('Foto do local do acidente.jpg.txt')
    const conteudo = readFileSync((await arquivo.path()) ?? '', 'utf-8')
    expect(conteudo).toContain('Documento de demonstração')
    expect(conteudo).toContain('não tem conteúdo real')
  })
})
