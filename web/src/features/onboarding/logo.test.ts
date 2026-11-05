import { describe, expect, it } from 'vitest'
import { MAX_LOGO_BYTES, validateLogo } from './logo'

const png = (extra = 0) =>
  new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, ...new Array(extra).fill(0)])], 'logo.png', {
    type: 'image/png',
  })
const svg = (body: string) => new File([`<svg xmlns="http://www.w3.org/2000/svg">${body}</svg>`], 'logo.svg', { type: 'image/svg+xml' })

describe('validateLogo', () => {
  it('aceita PNG com assinatura correta e SVG limpo', async () => {
    expect((await validateLogo(png())).ok).toBe(true)
    expect((await validateLogo(svg('<rect width="10" height="10"/>'))).ok).toBe(true)
  })

  it('rejeita PNG falso (extensão/MIME mentem)', async () => {
    const fake = new File(['não sou png'], 'x.png', { type: 'image/png' })
    expect(await validateLogo(fake)).toMatchObject({ ok: false })
  })

  it.each([
    '<script>alert(1)</script>',
    '<rect onload="x()"/>',
    '<a href="javascript:alert(1)"/>',
    '<foreignObject><div/></foreignObject>',
  ])('rejeita SVG com conteúdo ativo: %s', async (body) => {
    expect(await validateLogo(svg(body))).toMatchObject({ ok: false })
  })

  it('rejeita tipo não permitido, vazio e acima de 2 MB', async () => {
    expect(await validateLogo(new File(['x'], 'a.gif', { type: 'image/gif' }))).toMatchObject({ ok: false })
    expect(await validateLogo(new File([], 'a.png', { type: 'image/png' }))).toMatchObject({ ok: false })
    const big = new File([new Uint8Array(MAX_LOGO_BYTES + 1)], 'a.png', { type: 'image/png' })
    expect(await validateLogo(big)).toMatchObject({ ok: false, error: expect.stringContaining('2 MB') })
  })

  it('logo grande demais para o navegador não é "persistível"', async () => {
    const result = await validateLogo(png(600 * 1024))
    expect(result).toMatchObject({ ok: true, persistable: false })
  })
})
