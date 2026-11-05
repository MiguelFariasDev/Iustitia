import { env } from '@/config/env'
import { AccessGuard } from './AccessGuard'
import { AppBootstrap } from './AppBootstrap'
import { CommandPaletteLoader } from './CommandPaletteLoader'
import { MobileNav } from './MobileNav'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

/** Shell autenticado: sidebar + topbar + conteúdo. Server Component; ilhas client internas. */
export function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh bg-muted/30">
      <Sidebar />
      <MobileNav />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />
        <main id="conteudo" tabIndex={-1} className="flex-1 px-4 py-6 outline-none sm:px-6 lg:px-8">
          <div className="mx-auto w-full max-w-7xl">
            <AccessGuard>{children}</AccessGuard>
          </div>
        </main>
        <footer className="border-t px-4 py-4 text-xs text-muted-foreground sm:px-6 lg:px-8">
          <div className="mx-auto flex max-w-7xl flex-col gap-2">
            <div className="flex flex-wrap justify-between gap-2">
              <span>
                © {new Date().getFullYear()} {env.appName}
              </span>
              <span>Sugestões de IA exigem revisão humana antes de qualquer ação.</span>
            </div>
            {/*
              Exigido pela decisão registrada na ADR-064: o DataJud proíbe uso comercial, e o
              aviso é parte do que torna este uso defensável. Não remover.
            */}
            <p>
              Projeto de portfólio. Não comercializado. Dados de processos obtidos do DataJud (CNJ)
              sob os termos de uso da API Pública.
            </p>
          </div>
        </footer>
      </div>
      <AppBootstrap />
      <CommandPaletteLoader />
    </div>
  )
}
