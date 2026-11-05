'use client'

import { Menu } from 'lucide-react'
import Link from 'next/link'
import { useEffect, useState } from 'react'
import { BrandMark } from '@/components/iu/BrandMark'
import { ThemeToggle } from '@/components/iu/ThemeToggle'
import { Button } from '@/components/ui/button'
import { Sheet, SheetClose, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { routes } from '@/config/routes'
import { cn } from '@/lib/utils'
import { navLinks } from './content'

export function LandingHeader() {
  const [scrolled, setScrolled] = useState(false)

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  return (
    <header
      className={cn(
        'sticky top-0 z-40 border-b border-transparent backdrop-blur transition-colors',
        'bg-[color-mix(in_srgb,var(--iu-bg)_82%,transparent)]',
        scrolled && 'border-border'
      )}
    >
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-6 px-4 sm:px-6">
        <Link href="/" aria-label="Início" className="rounded-md">
          <BrandMark className="text-lg" />
        </Link>

        <nav aria-label="Seções" className="hidden items-center gap-1 lg:flex">
          {navLinks.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          <ThemeToggle />
          <Button asChild variant="ghost" className="hidden sm:inline-flex">
            <Link href={routes.login}>Entrar</Link>
          </Button>
          <Button asChild className="hidden sm:inline-flex">
            <Link href={routes.signup}>Começar grátis</Link>
          </Button>

          <Sheet>
            <SheetTrigger asChild>
              <Button variant="outline" size="icon" className="lg:hidden" aria-label="Abrir menu">
                <Menu aria-hidden />
              </Button>
            </SheetTrigger>
            <SheetContent side="right" className="iu w-72">
              <SheetHeader>
                <SheetTitle>Menu</SheetTitle>
              </SheetHeader>
              <nav aria-label="Menu" className="flex flex-col gap-1 px-4">
                {navLinks.map((link) => (
                  <SheetClose asChild key={link.href}>
                    <a href={link.href} className="rounded-md px-3 py-3 text-sm hover:bg-accent">
                      {link.label}
                    </a>
                  </SheetClose>
                ))}
                <Button asChild variant="outline" className="mt-4 h-11">
                  <Link href={routes.login}>Entrar</Link>
                </Button>
                <Button asChild className="h-11">
                  <Link href={routes.signup}>Começar grátis</Link>
                </Button>
              </nav>
            </SheetContent>
          </Sheet>
        </div>
      </div>
    </header>
  )
}
