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

function show(kind: NewNotification['kind'], title: string, options: NotifyOptions = {}) {
  toast[kind](title, { description: options.description, action: options.action })
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
