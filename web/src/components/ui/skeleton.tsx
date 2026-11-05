import { cn } from '@/lib/utils'

function Skeleton({ className, ...props }: React.ComponentProps<'div'>) {
  return (
    <div
      data-slot="skeleton"
      className={cn('animate-iu-pulse rounded-md bg-border', className)}
      {...props}
    />
  )
}

export { Skeleton }
