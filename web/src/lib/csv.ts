/**
 * Serialização CSV para exportações. Dois cuidados que planilhas exigem:
 *  - aspas e quebras de linha escapadas (RFC 4180);
 *  - neutralização de fórmula: uma célula que começa com `=`, `+`, `-`, `@` (ou tab/CR) é
 *    executada como fórmula pelo Excel/Sheets. Dados vindos de fora (nome de cliente, órgão)
 *    não são confiáveis — prefixar com apóstrofo impede que um valor vire comando.
 */

import { downloadBlob } from './download'
export function csvCell(value: string | number | boolean | null | undefined): string {
  let text = value === null || value === undefined ? '' : String(value)

  if (/^[=+\-@\t\r]/.test(text)) text = `'${text}`

  return /[",\n\r;]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

export function toCsv(header: string[], rows: (string | number | boolean | null | undefined)[][]) {
  // BOM para o Excel abrir UTF-8 com acentos corretamente; `;` é o separador do Excel pt-BR.
  const lines = [header, ...rows].map((row) => row.map(csvCell).join(';'))
  return `﻿${lines.join('\r\n')}`
}

/** Dispara o download no navegador sem passar por servidor. */
export function downloadCsv(filename: string, content: string) {
  downloadBlob(filename, new Blob([content], { type: 'text/csv;charset=utf-8' }))
}
