import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { ApiError } from '@/lib/api/errors'
import { ErrorBoundary } from './ErrorBoundary'

function Bomb({ error }: { error: unknown }): React.ReactNode {
  throw error
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // React registra o erro capturado no console; silenciamos para manter a saída limpa.
    vi.spyOn(console, 'error').mockImplementation(() => undefined)
  })

  it('renderiza os filhos quando não há erro', () => {
    render(
      <ErrorBoundary>
        <p>conteúdo</p>
      </ErrorBoundary>
    )
    expect(screen.getByText('conteúdo')).toBeInTheDocument()
  })

  it('mostra fallback amigável sem expor a mensagem técnica do erro', () => {
    render(
      <ErrorBoundary title="Falha no card">
        <Bomb error={new Error('TypeError: x is undefined')} />
      </ErrorBoundary>
    )
    expect(screen.getByRole('alert')).toHaveTextContent('Falha no card')
    expect(screen.queryByText(/x is undefined/)).not.toBeInTheDocument()
  })

  it('exibe a mensagem e o código de suporte de um ApiError', () => {
    render(
      <ErrorBoundary>
        <Bomb error={new ApiError(403, { detail: 'Sem permissão.', correlationId: 'abc-123' })} />
      </ErrorBoundary>
    )
    expect(screen.getByText('Sem permissão.')).toBeInTheDocument()
    expect(screen.getByText(/abc-123/)).toBeInTheDocument()
  })

  it('"Tentar novamente" reseta o boundary e chama onReset', async () => {
    const onReset = vi.fn()
    function Harness() {
      const [broken, setBroken] = useState(true)
      return (
        <ErrorBoundary
          onReset={() => {
            onReset()
            setBroken(false)
          }}
        >
          {broken ? <Bomb error={new Error('boom')} /> : <p>recuperado</p>}
        </ErrorBoundary>
      )
    }
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: /tentar novamente/i }))
    expect(onReset).toHaveBeenCalledOnce()
    expect(screen.getByText('recuperado')).toBeInTheDocument()
  })
})
