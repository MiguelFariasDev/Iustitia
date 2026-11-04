import type { Metadata } from 'next'

import { ProfileSettings } from '@/features/settings/components/ProfileSettings'

export const metadata: Metadata = { title: 'Perfil' }

export default function ProfilePage() {
  return <ProfileSettings />
}
