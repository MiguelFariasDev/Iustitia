import Link from 'next/link'
import { BrandMark } from '@/components/iu/BrandMark'
import { routes } from '@/config/routes'

export const TERMS_VERSION = '0.1-rascunho'

/**
 * Casca das páginas legais. O TEXTO jurídico não é gerado aqui: precisa ser redigido/revisado por
 * responsável jurídico (LGPD, OAB). Enquanto isso, as páginas existem — o aceite do cadastro
 * precisa de links reais — e dizem claramente que o conteúdo está pendente.
 */
export function LegalPage({ title, sections }: { title: string; sections: { heading: string; hint: string }[] }) {
  return (
    <main id="conteudo" tabIndex={-1} className="mx-auto max-w-3xl px-4 py-10 outline-none sm:px-6">
      <Link href={routes.home} aria-label="Voltar ao início">
        <BrandMark />
      </Link>
      <h1 className="mt-10 text-3xl font-semibold">{title}</h1>
      <p role="note" className="mt-4 rounded-[10px] border border-[var(--iu-warn)] p-3 text-sm">
        {'{{PREENCHER}}'} Texto jurídico pendente de redação e revisão. Versão {TERMS_VERSION}. Esta página
        ainda não vale como documento contratual.
      </p>
      <div className="mt-8 space-y-6">
        {sections.map((section) => (
          <section key={section.heading}>
            <h2 className="text-lg font-semibold">{section.heading}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{'{{PREENCHER}}'} {section.hint}</p>
          </section>
        ))}
      </div>
    </main>
  )
}
