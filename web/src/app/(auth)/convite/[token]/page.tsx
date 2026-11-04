import type { Metadata } from 'next'
import { InviteForm } from '@/features/auth/components/InviteForm'

export const metadata: Metadata = { title: 'Aceitar convite' }

export default async function InvitePage({ params }: PageProps<'/convite/[token]'>) {
  const { token } = await params
  return <InviteForm token={token} />
}
