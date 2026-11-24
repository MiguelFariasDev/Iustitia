import { toast } from 'sonner'
import { useNotificationStore, type NewNotification } from '@/stores/notificationStore'

interface NotifyOptions {
  description?: string
  /** Também registra no sino (notificação persistente). */
  persist?: boolean
  href?: string
  /** Ação no próprio toast — usada para oferecer "Desfazer" em ações destrutivas. */
  action?: { label: string; onClick: () => void }
}

/** 2,4 s, como no protótipo. Erro e toast com ação ficam mais: dá tempo de ler e de clicar. */
const DEFAULT_DURATION_MS = 2400
const LONG_DURATION_MS = 6000

function show(kind: NewNotification['kind'], title: string, options: NotifyOptions = {}) {
  toast[kind](title, {
    description: options.description,
    action: options.action,
    duration: kind === 'error' || options.action ? LONG_DURATION_MS : DEFAULT_DURATION_MS,
  })
  if (options.persist) {
    useNotificationStore.getState().add({
      kind,
      title,
      description: options.description,
      href: options.href,
    })
  }
}

/** API única de feedback in-app: toast (sonner) + opcionalmente o sino. */
export const notify = {
  success: (title: string, options?: NotifyOptions) => show('success', title, options),
  error: (title: string, options?: NotifyOptions) => show('error', title, options),
  warning: (title: string, options?: NotifyOptions) => show('warning', title, options),
  info: (title: string, options?: NotifyOptions) => show('info', title, options),
}
