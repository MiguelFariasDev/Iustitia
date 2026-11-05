import { Info } from 'lucide-react'

/** Por que pedimos cada dado (LGPD: finalidade clara, coletar o mínimo necessário). */
export function HelpCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <aside className="iu-card h-fit space-y-2 p-5 text-sm" aria-label="Por que pedimos isto">
      <p className="flex items-center gap-2 font-semibold">
        <Info className="size-4 text-[var(--iu-accent)]" aria-hidden /> {title}
      </p>
      <div className="space-y-2 leading-relaxed text-muted-foreground">{children}</div>
    </aside>
  )
}
