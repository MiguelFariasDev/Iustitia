import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { reiniciarArmazens } from '@/lib/storage/armazem'
import { navigationState, routerMock } from './navigation'

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  navigationState.pathname = '/painel'
  navigationState.searchParams = new URLSearchParams()
  document.cookie = 'adv-mock-session=; Path=/; Max-Age=0'
  localStorage.clear()
  reiniciarArmazens()
})

// App Router: navegação mockada e inspecionável nos testes (ver ./navigation).
vi.mock('next/navigation', () => ({
  useRouter: () => routerMock,
  usePathname: () => navigationState.pathname,
  useSearchParams: () => navigationState.searchParams,
  redirect: vi.fn(),
}))

// jsdom não implementa estas APIs usadas por Radix/cmdk/media queries.
if (!window.matchMedia) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    }),
  })
}

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
globalThis.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver
Element.prototype.scrollIntoView ??= vi.fn()
Element.prototype.hasPointerCapture ??= vi.fn(() => false)
Element.prototype.releasePointerCapture ??= vi.fn()
