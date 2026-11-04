'use client'

import { Component, type ErrorInfo, type ReactNode } from 'react'
import { reportError } from '@/lib/observability/reportError'
import { ErrorFallback } from './ErrorFallback'

interface ErrorBoundaryProps {
  children: ReactNode
  /** Fallback customizado; recebe o erro e uma função para tentar novamente. */
  fallback?: (props: { error: unknown; reset: () => void }) => ReactNode
  onReset?: () => void
  title?: string
}

interface ErrorBoundaryState {
  error: unknown
  hasError: boolean
}

/**
 * Isola falhas de renderização de uma seção (ex.: um card do painel) sem derrubar a página.
 * Para erros de rota inteira, o App Router usa error.tsx.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null, hasError: false }

  static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
    return { error, hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    reportError(error, { componentStack: info.componentStack })
  }

  reset = () => {
    this.props.onReset?.()
    this.setState({ error: null, hasError: false })
  }

  render() {
    if (!this.state.hasError) return this.props.children
    if (this.props.fallback)
      return this.props.fallback({ error: this.state.error, reset: this.reset })
    return <ErrorFallback error={this.state.error} onRetry={this.reset} title={this.props.title} />
  }
}
