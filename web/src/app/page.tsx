import { redirect } from 'next/navigation'
import { routes } from '@/config/routes'

/** O proxy já manda visitantes para /login; aqui só resta o usuário autenticado. */
export default function HomePage() {
  redirect(routes.dashboard)
}
