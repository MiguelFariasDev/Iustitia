'use client'

import { Menu, Search } from 'lucide-react'
import Link from 'next/link'
import { Logo } from '@/components/shared/Logo'
import { Button } from '@/components/ui/button'
import { routes } from '@/config/routes'
import { useUiStore } from '@/stores/uiStore'
import { Breadcrumbs } from './Breadcrumbs'
import { CreateButton } from './CreateButton'
import { NotificationsDropdown } from './NotificationsDropdown'
import { UserMenu } from './UserMenu'

export function Topbar() {
  const setMobileNavOpen = useUiStore((s) => s.setMobileNavOpen)
  const openPalette = useUiStore((s) => s.setCommandPaletteOpen)

  return (
    <header className="sticky top-0 z-30 flex h-16 items-center gap-2 border-b bg-background/85 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/70 sm:gap-3 sm:px-6">
      <Button
        variant="ghost"
        size="icon"
        className="lg:hidden"
        onClick={() => setMobileNavOpen(true)}
        aria-label="Abrir menu de navegação"
      >
        <Menu aria-hidden />
      </Button>
      <Link href={routes.dashboard} className="rounded-md lg:hidden">
        <Logo showWordmark={false} size={28} />
      </Link>

      <Breadcrumbs className="hidden md:block" />

      <div className="ml-auto flex items-center gap-1.5 sm:gap-2">
        <Button
          variant="outline"
          onClick={() => openPalette(true)}
          aria-label="Buscar (Ctrl+K)"
          aria-keyshortcuts="Control+K Meta+K"
          className="h-10 gap-2 px-3 text-muted-foreground sm:w-[250px] sm:justify-start"
        >
          <Search aria-hidden />
          <span className="hidden sm:inline">Buscar…</span>
          <kbd className="pointer-events-none ml-auto hidden rounded-xs border bg-muted px-1.5 font-mono text-[10px] font-medium sm:inline-block">
            Ctrl K
          </kbd>
        </Button>
        <CreateButton />
        <NotificationsDropdown />
        <UserMenu />
      </div>
    </header>
  )
}
