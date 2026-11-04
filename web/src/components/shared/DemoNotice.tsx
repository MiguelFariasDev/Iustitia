interface DemoNoticeProps {
  /** Nome do módulo, para a frase ficar específica ("O módulo Contatos…"). */
  modulo: string
  /** O que ainda não funciona de verdade — dito sem rodeio. */
  children?: React.ReactNode
}

/**
 * Aviso fixo de que a tela usa dados de demonstração. Existe porque as telas de apoio foram
 * entregues antes do backend: um número que parece do escritório e não é leva a decisão
 * errada, então o aviso fica sempre visível (não é dispensável).
 */
export function DemoNotice({ modulo, children }: DemoNoticeProps) {
  return (
    <p
      role="note"
      className="rounded-lg border border-dashed px-3 py-2 text-sm text-muted-foreground"
    >
      <strong>Dados de demonstração.</strong> O módulo {modulo} ainda não está ligado ao servidor —{' '}
      {children ?? 'nada aqui é do seu escritório e as alterações se perdem ao recarregar.'}
    </p>
  )
}
