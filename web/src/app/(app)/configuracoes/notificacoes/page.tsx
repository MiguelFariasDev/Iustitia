import type { Metadata } from 'next'

import { NotificationSettings } from '@/features/settings/components/NotificationSettings'

export const metadata: Metadata = { title: 'Notificações' }

export default function NotificationsSettingsPage() {
  return <NotificationSettings />
}
