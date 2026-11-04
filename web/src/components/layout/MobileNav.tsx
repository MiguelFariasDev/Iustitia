'use client'

import { Logo } from '@/components/shared/Logo'
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { useUiStore } from '@/stores/uiStore'
import { SidebarNav } from './SidebarNav'

export function MobileNav() {
  const open = useUiStore((s) => s.mobileNavOpen)
  const setOpen = useUiStore((s) => s.setMobileNavOpen)

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetContent
        side="left"
        className="w-72 border-sidebar-border bg-sidebar p-0 text-sidebar-foreground [&>button]:text-sidebar-foreground"
      >
        <SheetHeader className="h-16 justify-center border-b border-sidebar-border px-4">
          <SheetTitle>
            <Logo variant="inverted" />
          </SheetTitle>
          <SheetDescription className="sr-only">Menu de navegação</SheetDescription>
        </SheetHeader>
        <div className="overflow-y-auto">
          <SidebarNav onNavigate={() => setOpen(false)} />
        </div>
      </SheetContent>
    </Sheet>
  )
}
