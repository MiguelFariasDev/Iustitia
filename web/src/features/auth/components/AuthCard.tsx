import { cn } from '@/lib/utils'

interface AuthCardProps {
  title: string
  description?: React.ReactNode
  children: React.ReactNode
  footer?: React.ReactNode
  className?: string
}

export function AuthCard({ title, description, children, footer, className }: AuthCardProps) {
  return (
    <div className={cn('iu-in w-full max-w-[420px] space-y-6', className)}>
      <div className="space-y-2">
        <h1 className="text-3xl font-semibold">{title}</h1>
        {description ? <p className="text-sm text-muted-foreground">{description}</p> : null}
      </div>
      {children}
      {footer ? <div className="text-center text-sm text-muted-foreground">{footer}</div> : null}
    </div>
  )
}
