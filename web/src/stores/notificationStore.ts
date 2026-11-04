import { create } from 'zustand'

export type NotificationKind = 'info' | 'success' | 'warning' | 'error'

export interface AppNotification {
  id: string
  kind: NotificationKind
  title: string
  description?: string
  /** ISO 8601 */
  createdAt: string
  read: boolean
  href?: string
}

export type NewNotification = Omit<AppNotification, 'id' | 'createdAt' | 'read'> &
  Partial<Pick<AppNotification, 'id' | 'createdAt' | 'read'>>

interface NotificationState {
  items: AppNotification[]
  add: (notification: NewNotification) => AppNotification
  setAll: (items: AppNotification[]) => void
  markAsRead: (id: string) => void
  markAllAsRead: () => void
  remove: (id: string) => void
  clear: () => void
}

/**
 * Notificações persistentes (sino). Toasts efêmeros usam `notify` (lib/notify.ts).
 * Na fase de Realtime, o canal do Supabase chamará `add` para cada evento recebido.
 */
export const useNotificationStore = create<NotificationState>()((set) => ({
  items: [],
  add: (input) => {
    const notification: AppNotification = {
      id: input.id ?? crypto.randomUUID(),
      createdAt: input.createdAt ?? new Date().toISOString(),
      read: input.read ?? false,
      kind: input.kind,
      title: input.title,
      description: input.description,
      href: input.href,
    }
    set((s) => ({ items: [notification, ...s.items] }))
    return notification
  },
  setAll: (items) => set({ items }),
  markAsRead: (id) =>
    set((s) => ({ items: s.items.map((n) => (n.id === id ? { ...n, read: true } : n)) })),
  markAllAsRead: () => set((s) => ({ items: s.items.map((n) => ({ ...n, read: true })) })),
  remove: (id) => set((s) => ({ items: s.items.filter((n) => n.id !== id) })),
  clear: () => set({ items: [] }),
}))

/** Seletor derivado (evita guardar contagem duplicada no estado). */
export const selectUnreadCount = (s: NotificationState) =>
  s.items.reduce((count, n) => (n.read ? count : count + 1), 0)
