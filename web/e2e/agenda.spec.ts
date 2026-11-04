import { expect, test } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

test.describe('Agenda', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/agenda')
    await waitForPageReady(page, 'Agenda')
  })

  test('mostra a grade da semana com os sete dias', async ({ page }) => {
    for (const day of ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb']) {
      await expect(page.getByText(day, { exact: true })).toBeVisible()
    }
  })

  test('mostra a régua de horas', async ({ page }) => {
    // Escopo na régua: os horários dos eventos usam o mesmo formato.
    const regua = page.locator('[data-slot="hour-gutter"]')
    await expect(regua.getByText('08:00', { exact: true })).toBeVisible()
    await expect(regua.getByText('18:00', { exact: true })).toBeVisible()
  })

  test('renderiza os eventos da semana', async ({ page }) => {
    await expect(
      page.getByRole('button', { name: /Audiência de conciliação — Maria Oliveira/ })
    ).toBeVisible()
    await expect(page.getByRole('button', { name: /Prazo fatal: contestação/ })).toBeVisible()
  })

  test('cada evento anuncia horário, tipo e local', async ({ page }) => {
    const evento = page.getByRole('button', {
      name: /Audiência de conciliação — Maria Oliveira/,
    })
    await expect(evento).toHaveAccessibleName(/09:00/)
    await expect(evento).toHaveAccessibleName(/Audiência/)
    await expect(evento).toHaveAccessibleName(/3ª Vara Cível de Porto Alegre/)
  })

  test('navega entre semanas e volta para hoje', async ({ page }) => {
    const titulo = page.getByRole('heading', { level: 2 }).first()
    const semanaAtual = await titulo.textContent()

    await page.getByRole('button', { name: 'Próxima semana' }).click()
    await expect(titulo).not.toHaveText(semanaAtual ?? '')

    await page.getByRole('button', { name: 'Hoje' }).click()
    await expect(titulo).toHaveText(semanaAtual ?? '')
  })

  test('filtra por "Minhas atribuições"', async ({ page }) => {
    await expect(page.getByRole('button', { name: /Audiência de instrução/ })).toBeVisible()

    await page.getByRole('button', { name: 'Minhas atribuições' }).click()

    await expect(page.getByRole('button', { name: /Audiência de instrução/ })).toBeHidden()
    await expect(page.getByRole('button', { name: /Audiência de conciliação/ })).toBeVisible()
  })

  test('destaca o dia de hoje', async ({ page }) => {
    await expect(page.getByText('(hoje)')).toBeAttached()
  })

  test('mostra a legenda de cores com rótulo textual', async ({ page }) => {
    const legenda = page.getByRole('list', { name: 'Legenda de cores' })
    for (const label of ['Audiência', 'Reunião', 'Evento', 'Tarefa', 'Prazo']) {
      await expect(legenda.getByText(label, { exact: true })).toBeVisible()
    }
  })

  test('cada dia é uma região nomeada para leitores de tela', async ({ page }) => {
    await expect(
      page.getByRole('region', { name: /-feira, \d+ de \w+ de \d{4}/ }).first()
    ).toBeAttached()
  })
})

test.describe('Agenda — primeiro acesso', () => {
  test('mostra e dispensa o aviso da semana', async ({ page }) => {
    await page.goto('/agenda')

    const hint = page.getByRole('heading', { name: 'Sua semana em uma tela' })
    await expect(hint).toBeVisible()

    await page.getByRole('button', { name: 'Entendi' }).click()
    await expect(hint).toBeHidden()
  })
})
