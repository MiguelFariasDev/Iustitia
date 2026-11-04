import { act, renderHook } from '@testing-library/react'
import { useUiStore } from '@/stores/uiStore'
import { resolveTheme, useTheme } from './useTheme'

describe('useTheme', () => {
  beforeEach(() => useUiStore.setState({ theme: 'system' }))

  it('resolveTheme segue o sistema quando "system"', () => {
    expect(resolveTheme('system', true)).toBe('dark')
    expect(resolveTheme('system', false)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })

  it('usa a preferência do sistema (claro no jsdom) e alterna para escuro', () => {
    const { result } = renderHook(() => useTheme())
    expect(result.current.resolvedTheme).toBe('light')
    act(() => result.current.toggleTheme())
    expect(result.current.theme).toBe('dark')
    expect(result.current.resolvedTheme).toBe('dark')
  })

  it('setTheme aceita light/dark/system', () => {
    const { result } = renderHook(() => useTheme())
    act(() => result.current.setTheme('light'))
    expect(useUiStore.getState().theme).toBe('light')
  })
})
