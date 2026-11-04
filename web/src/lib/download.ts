/**
 * Dispara o download de um Blob no navegador, sem passar por servidor.
 *
 * O link precisa estar ANEXADO ao documento antes do `click()`: sem isso o Chromium ignora o
 * atributo `download` e entrega o arquivo com o nome genérico "download". A URL é revogada
 * depois, num macrotask, para o navegador já ter iniciado a leitura do Blob.
 */
export function downloadBlob(filename: string, blob: Blob) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
