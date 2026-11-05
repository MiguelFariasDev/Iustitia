import { expect, test, type Page } from '@playwright/test'
import { dismissOnboardingHints, waitForPageReady } from './helpers'

/**
 * Inbox de atendimentos (WhatsApp). A API simulada tem 5 conversas: 2 Aguardando (Maria, João),
 * 1 Humano de outro advogado (Construtora Alfa), 1 Bot (sem nome) e 1 Encerrada (Ana).
 *
 * Sem Supabase, o canal Realtime é trocado pelo evento de janela `adv:atendimento-client-message`
 * (ver features/atendimentos/realtime.ts): simula o cliente escrevendo — e o Bot reagindo — e passa
 * pelo MESMO tratamento do broadcast real (invalidação do cache e toast).
 */
async function clientWrites(
  page: Page,
  detail: { phoneNumber?: string; atendimentoId?: string; text: string }
) {
  await page.evaluate(
    (payload) =>
      window.dispatchEvent(new CustomEvent('adv:atendimento-client-message', { detail: payload })),
    detail
  )
}

const fila = (page: Page) => page.getByRole('region', { name: 'Conversas' })
const conversa = (page: Page) => page.getByRole('region', { name: 'Conversa' })
const campo = (page: Page) => page.getByLabel('Mensagem para o cliente')

test.describe('Atendimentos', () => {
  test.beforeEach(async ({ page }) => {
    await dismissOnboardingHints(page)
    await page.goto('/atendimentos')
    await waitForPageReady(page, 'Atendimentos')
    await expect(fila(page).getByRole('list')).toBeVisible()
  })

  test('mostra a fila com um badge por status, quem espera um humano primeiro', async ({
    page,
  }) => {
    const itens = fila(page).getByRole('listitem')

    await expect(itens).toHaveCount(5)
    await expect(itens.first()).toContainText('Maria Souza')
    await expect(itens.first()).toContainText('Aguardando')
    // Dentro da lista: "Humano" e "Bot" também são nomes de abas, e o badge é que está sob teste.
    const lista = fila(page).getByRole('list')
    await expect(lista.getByText('Humano', { exact: true })).toBeVisible()
    await expect(lista.getByText('Bot', { exact: true })).toBeVisible()
    await expect(lista.getByText('Encerrado', { exact: true })).toBeVisible()
  })

  test('filtra por aba e busca por nome', async ({ page }) => {
    await page.getByRole('tab', { name: 'Aguardando' }).click()
    await expect(fila(page).getByRole('listitem')).toHaveCount(2)

    await page.getByRole('tab', { name: 'Todos' }).click()
    await page.getByLabel('Buscar conversa').fill('construtora')
    await expect(fila(page).getByRole('listitem')).toHaveCount(1)
    await expect(fila(page)).toContainText('Construtora Alfa')
  })

  test('fluxo completo: assumir, responder, devolver ao bot e encerrar', async ({ page }) => {
    await fila(page)
      .getByRole('button', { name: /Maria Souza/ })
      .click()
    await expect(page).toHaveURL(/c=atd-001/)

    // Aguardando: o campo está bloqueado e diz por quê.
    await expect(campo(page)).toBeDisabled()
    await expect(
      conversa(page).getByText('Assuma o atendimento para responder ao cliente.')
    ).toBeVisible()
    await expect(conversa(page).getByText('Aguarde, um advogado vai te atender.')).toBeVisible()

    // Assumir libera o campo.
    await conversa(page).getByRole('button', { name: 'Assumir atendimento' }).click()
    await expect(campo(page)).toBeEnabled()
    await expect(conversa(page).getByRole('button', { name: 'Devolver ao bot' })).toBeVisible()

    // Responder: Enter envia.
    await campo(page).fill('Bom dia, Maria. Já estou vendo o seu caso.')
    await campo(page).press('Enter')
    await expect(
      conversa(page)
        .getByRole('log', { name: 'Mensagens da conversa' })
        .getByText('Bom dia, Maria. Já estou vendo o seu caso.')
    ).toBeVisible()
    await expect(campo(page)).toHaveValue('')

    // Devolver ao bot bloqueia o campo de novo.
    await conversa(page).getByRole('button', { name: 'Devolver ao bot' }).click()
    await expect(campo(page)).toBeDisabled()
    await expect(conversa(page).getByText('O bot está conduzindo esta conversa.')).toBeVisible()

    // Encerrar pede confirmação.
    await conversa(page).getByRole('button', { name: 'Encerrar atendimento' }).click()
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: 'Encerrar atendimento' })
      .click()
    await expect(conversa(page).getByText('Atendimento encerrado.')).toBeVisible()
    await expect(campo(page)).toBeDisabled()
  })

  test('conversa de outro advogado: não se pode escrever nem tomar', async ({ page }) => {
    await fila(page)
      .getByRole('button', { name: /Construtora Alfa/ })
      .click()

    await expect(campo(page)).toBeDisabled()
    await expect(conversa(page).getByText('Esta conversa está com outro advogado.')).toBeVisible()
    await expect(conversa(page).getByRole('button', { name: 'Assumir atendimento' })).toHaveCount(0)
    await expect(conversa(page).getByRole('button', { name: 'Devolver ao bot' })).toHaveCount(0)
  })

  test('a conversa aberta fica na URL: recarregar mantém o lugar', async ({ page }) => {
    await fila(page)
      .getByRole('button', { name: /João Pereira/ })
      .click()
    await expect(page).toHaveURL(/c=atd-002/)

    await page.reload()

    await expect(conversa(page).getByRole('heading', { name: 'João Pereira' })).toBeVisible()
  })

  test('tempo real: o cliente pede um humano e o advogado é avisado, sem recarregar', async ({
    page,
  }) => {
    // A conversa "sem nome" está com o bot.
    await expect(fila(page).getByRole('tab', { name: 'Aguardando' })).toBeVisible()
    await page.getByRole('tab', { name: 'Aguardando' }).click()
    await expect(fila(page).getByRole('listitem')).toHaveCount(2)

    // O cliente digita "2": o bot pede o handoff e avisa o cliente.
    await clientWrites(page, { phoneNumber: '5585966665555', text: '2' })

    await expect(page.getByText('Um cliente está aguardando atendimento.').first()).toBeVisible()
    await expect(fila(page).getByRole('listitem')).toHaveCount(3)
  })

  test('tempo real: mensagem nova aparece na conversa aberta', async ({ page }) => {
    await fila(page)
      .getByRole('button', { name: /Maria Souza/ })
      .click()
    const log = conversa(page).getByRole('log', { name: 'Mensagens da conversa' })
    await expect(log).toContainText('Bom dia, preciso falar sobre a audiência de sexta.')
    const menusDoBotAntes = await log.getByText('Como posso ajudar?').count()

    await clientWrites(page, { atendimentoId: 'atd-001', text: 'Doutora, é urgente!' })

    await expect(log.getByText('Doutora, é urgente!')).toBeVisible()
    // Aguardando: o bot NÃO responde. O menu da conversa semeada continua sendo o único.
    await expect(log.getByText('Como posso ajudar?')).toHaveCount(menusDoBotAntes)
  })

  test('o contexto mostra o contato e oferece vincular quando não há cadastro', async ({
    page,
  }) => {
    await fila(page)
      .getByRole('button', { name: /Maria Souza/ })
      .click()

    const contexto = page.getByLabel('Contexto do atendimento')
    await expect(contexto.getByText('+55 (85) 99999-8888')).toBeVisible()
    await expect(contexto.getByText('Contato sem cadastro')).toBeVisible()
    await expect(contexto.getByLabel('Buscar cliente para vincular')).toBeVisible()
  })
})
