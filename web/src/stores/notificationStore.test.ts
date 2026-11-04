import { selectUnreadCount, useNotificationStore } from './notificationStore'

describe('notificationStore', () => {
  beforeEach(() => useNotificationStore.getState().clear())

  it('adiciona no topo, como não lida', () => {
    const { add } = useNotificationStore.getState()
    add({ kind: 'info', title: 'Primeira' })
    add({ kind: 'warning', title: 'Segunda' })
    const { items } = useNotificationStore.getState()
    expect(items.map((n) => n.title)).toEqual(['Segunda', 'Primeira'])
    expect(selectUnreadCount(useNotificationStore.getState())).toBe(2)
  })

  it('marca uma / todas como lidas e remove', () => {
    const a = useNotificationStore.getState().add({ kind: 'info', title: 'A' })
    const b = useNotificationStore.getState().add({ kind: 'info', title: 'B' })
    useNotificationStore.getState().markAsRead(a.id)
    expect(selectUnreadCount(useNotificationStore.getState())).toBe(1)
    useNotificationStore.getState().markAllAsRead()
    expect(selectUnreadCount(useNotificationStore.getState())).toBe(0)
    useNotificationStore.getState().remove(b.id)
    expect(useNotificationStore.getState().items).toHaveLength(1)
  })
})
