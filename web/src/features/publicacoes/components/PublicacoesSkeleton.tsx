import { Skeleton } from '@/components/ui/skeleton'

export function PublicacoesSkeleton() {
  return (
    <div className="space-y-6" role="status" aria-label="Carregando publicações">
      <div className="grid gap-4 sm:grid-cols-3">
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-32 rounded-xl" />
        <Skeleton className="h-32 rounded-xl" />
      </div>
      <Skeleton className="h-10 w-full max-w-md rounded-md" />
      <Skeleton className="h-96 rounded-xl" />
      <span className="sr-only">Carregando publicações…</span>
    </div>
  )
}
