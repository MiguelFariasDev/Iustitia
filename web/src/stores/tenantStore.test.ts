import { featureFlagKeys, useTenantStore } from './tenantStore'

describe('tenantStore', () => {
  beforeEach(() => useTenantStore.getState().clear())

  it('feature flags são fail-closed (ausente = desabilitada), como no backend', () => {
    const store = useTenantStore.getState()
    expect(store.isFeatureEnabled(featureFlagKeys.athenaAssistant)).toBe(false)
    store.setFeatureFlags({ 'athena.assistant': true, 'legal.module': false })
    expect(useTenantStore.getState().isFeatureEnabled('athena.assistant')).toBe(true)
    expect(useTenantStore.getState().isFeatureEnabled('legal.module')).toBe(false)
    expect(useTenantStore.getState().isFeatureEnabled('kanban.board')).toBe(false)
  })

  it('clear remove o tenant e as flags (logout)', () => {
    useTenantStore.getState().setTenant({ id: '1', name: 'X', slug: 'x', plan: 'trial' })
    useTenantStore.getState().setFeatureFlags({ 'public.api': true })
    useTenantStore.getState().clear()
    expect(useTenantStore.getState()).toMatchObject({ currentTenant: null, featureFlags: {} })
  })
})
