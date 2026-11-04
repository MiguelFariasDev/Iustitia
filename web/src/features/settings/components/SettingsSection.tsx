import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

interface SettingsSectionProps {
  title: string
  description?: string
  children: React.ReactNode
  footer?: React.ReactNode
}

export function SettingsSection({ title, description, children, footer }: SettingsSectionProps) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">
          <h2>{title}</h2>
        </CardTitle>
        {description ? <CardDescription>{description}</CardDescription> : null}
      </CardHeader>
      <CardContent>{children}</CardContent>
      {footer ? (
        <CardFooter className="justify-end gap-2 border-t pt-6">{footer}</CardFooter>
      ) : null}
    </Card>
  )
}
