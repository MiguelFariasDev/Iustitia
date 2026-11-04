import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

/**
 * Inteiro teor. Renderizado como TEXTO (React escapa), nunca como HTML: o conteúdo vem de
 * uma fonte externa e o backend já remove a marcação, mas a defesa final é não interpretar.
 * `tabIndex=0` torna a região rolável alcançável por teclado.
 */
export function PublicacaoTextoCard({ content }: { content: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">
          <h2 id="publicacao-texto">Texto da publicação</h2>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div
          role="region"
          aria-labelledby="publicacao-texto"
          tabIndex={0}
          className="max-h-[32rem] overflow-y-auto rounded-md border bg-muted/30 p-4 text-sm leading-relaxed whitespace-pre-wrap focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none"
        >
          {content}
        </div>
      </CardContent>
    </Card>
  )
}
