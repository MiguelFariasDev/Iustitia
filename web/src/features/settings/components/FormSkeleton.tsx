import { Skeleton } from '@/components/ui/skeleton'

export function FormSkeleton({ fields = 4 }: { fields?: number }) {
  return (
    <div role="status" aria-label="Carregando" className="space-y-4 rounded-xl border bg-card p-6">
      <Skeleton className="h-5 w-40" />
      {Array.from({ length: fields }, (_, i) => (
        <div key={i} className="space-y-2">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-9 w-full" />
        </div>
      ))}
      <span className="sr-only">Carregando…</span>
    </div>
  )
}
