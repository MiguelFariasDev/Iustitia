import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
    env: {
      NEXT_PUBLIC_APP_NAME: 'Iustitia',
      NEXT_PUBLIC_AUTH_MODE: 'mock',
      NEXT_PUBLIC_API_MOCKS: 'true',
      NEXT_PUBLIC_API_URL: 'http://api.test/api/v1',
    },
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/components/ui/**', 'src/**/*.test.{ts,tsx}', 'src/test/**', 'src/app/**', 'src/**/mocks.ts', 'src/mocks/**'],
      reporter: ['text-summary', 'html'],
    },
  },
})
