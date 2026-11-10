import { cn } from '@/lib/utils'

/**
 * Seção de painel com título, para blocos de lista. `demo` põe o selo "Demonstração": o bloco usa
 * dados de `demo.ts` (sem backend), e quem decide em cima de um número precisa saber disso.
 */
export function DemoSection({
  title,
  demo = false,
  children,
  className,
  id,
}: {
  title: string
  demo?: boolean
  children: React.ReactNode
  className?: string
  id: string
}) {
  return (
    <section aria-labelledby={id} className={cn('space-y-3', className)}>
      <div className="flex items-center gap-2">
        <h2 id={id} className="text-lg font-semibold">
          {title}
        </h2>
        {demo ? (
          <span className="rounded-full border px-2 py-px text-[10px] font-medium tracking-wide text-muted-foreground uppercase">
            Demonstração
          </span>
        ) : null}
      </div>
      <div className="rounded-[10px] border bg-card p-4 shadow-sm">{children}</div>
    </section>
  )
}
