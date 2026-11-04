import { redirect } from 'next/navigation'
import { routes } from '@/config/routes'

export default function SettingsIndexPage() {
  redirect(routes.settings.profile)
}
