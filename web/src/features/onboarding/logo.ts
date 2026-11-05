export const MAX_LOGO_BYTES = 2 * 1024 * 1024
/** Acima disto o logotipo NÃO é guardado no navegador (limite do localStorage); só a prévia da sessão. */
export const MAX_LOGO_PERSIST_BYTES = 512 * 1024

export type LogoResult = { ok: true; dataUrl: string; persistable: boolean } | { ok: false; error: string }

const PNG_MAGIC = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]

function readAsDataUrl(file: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(String(reader.result))
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}

/**
 * Valida o logotipo por TIPO declarado, TAMANHO e CONTEÚDO (não confia na extensão nem no MIME):
 * PNG precisa da assinatura PNG; SVG precisa ser texto `<svg` e NÃO pode conter script, handlers
 * `on*=` nem `javascript:`/`<foreignObject>`. Mesmo assim ele só é exibido via `<img>` (que não
 * executa script). Importante: a validação definitiva de upload é do servidor (ainda inexistente).
 */
export async function validateLogo(file: File): Promise<LogoResult> {
  if (file.size === 0) return { ok: false, error: 'O arquivo está vazio.' }
  if (file.size > MAX_LOGO_BYTES) return { ok: false, error: 'O logotipo deve ter no máximo 2 MB.' }

  const head = new Uint8Array(await file.slice(0, 8).arrayBuffer())

  if (file.type === 'image/png') {
    if (!PNG_MAGIC.every((byte, index) => head[index] === byte)) {
      return { ok: false, error: 'O arquivo não é um PNG válido.' }
    }
  } else if (file.type === 'image/svg+xml') {
    const text = await file.text()
    if (!/^\s*(<\?xml[^>]*>\s*)?(<!--[\s\S]*?-->\s*)*<svg[\s>]/i.test(text)) {
      return { ok: false, error: 'O arquivo não é um SVG válido.' }
    }
    if (/<script|<foreignObject|\son\w+\s*=|javascript:|<iframe|<embed/i.test(text)) {
      return { ok: false, error: 'Este SVG contém elementos não permitidos (scripts ou conteúdo ativo).' }
    }
  } else {
    return { ok: false, error: 'Envie um arquivo PNG ou SVG.' }
  }

  return { ok: true, dataUrl: await readAsDataUrl(file), persistable: file.size <= MAX_LOGO_PERSIST_BYTES }
}
