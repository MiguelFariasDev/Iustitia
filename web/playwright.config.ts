import { defineConfig, devices } from '@playwright/test'

const PORT = Number(process.env.E2E_PORT ?? 3100)
const baseURL = `http://127.0.0.1:${PORT}`

/** Estado de sessão autenticada, produzido pelo projeto "setup". */
export const STORAGE_STATE = 'playwright/.auth/user.json'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  timeout: 30_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    // O produto é pt-BR: datas, moedas e ordenação dependem disso.
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
  },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], storageState: STORAGE_STATE },
      dependencies: ['setup'],
    },
  ],
  webServer: {
    // Build de produção em diretório próprio: mais fiel ao que vai ao ar e não
    // conflita com um `next dev` já em execução (que trava a porta e o .next).
    command: `npx next build && npx next start --port ${PORT}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    stdout: 'ignore',
    stderr: 'pipe',
    env: {
      // E2E roda sem Supabase e sem a API .NET: autenticação e dados simulados.
      NEXT_PUBLIC_APP_NAME: 'Iustitia',
      NEXT_PUBLIC_AUTH_MODE: 'mock',
      NEXT_PUBLIC_API_MOCKS: 'true',
      NEXT_PUBLIC_API_URL: 'http://127.0.0.1:5000/api/v1',
      NEXT_DIST_DIR: '.next-e2e',
    },
  },
})
