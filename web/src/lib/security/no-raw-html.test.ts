import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * Guarda de XSS: o React escapa tudo por padrão; `dangerouslySetInnerHTML` é a única porta para
 * HTML cru. Hoje só o script de tema do layout (conteúdo constante, sem dado de usuário) a usa.
 * Qualquer novo uso falha aqui e exige decisão consciente: sanitizar com DOMPurify E acrescentar
 * o arquivo à lista abaixo, na revisão de código (ADR-070).
 */
const ALLOWED = new Set(['app/layout.tsx'])

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return sourceFiles(path)
    return /\.(ts|tsx)$/.test(name) && !/\.test\./.test(name) ? [path] : []
  })
}

describe('HTML cru', () => {
  it('dangerouslySetInnerHTML só aparece nos arquivos permitidos', () => {
    const root = join(process.cwd(), 'src')
    const offenders = sourceFiles(root)
      .filter((file) => readFileSync(file, 'utf8').includes('dangerouslySetInnerHTML'))
      .map((file) => relative(root, file).split('\\').join('/'))
      .filter((file) => !ALLOWED.has(file))
    expect(offenders).toEqual([])
  })

  it('nenhum token fica em localStorage/sessionStorage', () => {
    const root = join(process.cwd(), 'src')
    const offenders = sourceFiles(root).filter((file) => {
      const text = readFileSync(file, 'utf8')
      return /(local|session)Storage\.(set|get)Item\([^)]*(token|jwt|access|refresh)/i.test(text)
    })
    expect(offenders.map((f) => relative(root, f))).toEqual([])
  })
})
