/**
 * Soma `dias` dias ÚTEIS a `inicio` pelo calendário forense NACIONAL: pula sábados, domingos e feriados
 * nacionais, e suspende a contagem no recesso (20/12 a 20/01, CPC art. 220). Exclui o dia do começo
 * (CPC art. 224). Espelha `CalculadoraPrazo` do backend (POST /prazos/calcular), que é a versão de referência.
 *
 * ATENÇÃO: NÃO conhece feriados estaduais/municipais, Corpus Christi, Carnaval, Semana Santa de cada
 * tribunal nem suspensões de prazo. É uma estimativa para o advogado conferir — nunca a fonte de um prazo
 * processual. A tela diz isso ao lado da data.
 */

/** Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher). */
export function pascoa(ano: number): Date {
  const a = ano % 19
  const b = Math.floor(ano / 100)
  const c = ano % 100
  const d = Math.floor(b / 4)
  const e = b % 4
  const f = Math.floor((b + 8) / 25)
  const g = Math.floor((b - f + 1) / 3)
  const h = (19 * a + b - d - g + 15) % 30
  const i = Math.floor(c / 4)
  const k = c % 4
  const l = (32 + 2 * e + 2 * i - h - k) % 7
  const m = Math.floor((a + 11 * h + 22 * l) / 451)
  const mes = Math.floor((h + l - 7 * m + 114) / 31)
  const dia = ((h + l - 7 * m + 114) % 31) + 1

  return new Date(ano, mes - 1, dia)
}

const FERIADOS_FIXOS: ReadonlyArray<readonly [number, number]> = [
  [1, 1], // Confraternização Universal
  [4, 21], // Tiradentes
  [5, 1], // Dia do Trabalho
  [9, 7], // Independência
  [10, 12], // Nossa Senhora Aparecida
  [11, 2], // Finados
  [11, 15], // Proclamação da República
  [12, 25], // Natal
]

const chave = (d: Date) => d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate()

export function isFeriadoNacional(dia: Date): boolean {
  const ano = dia.getFullYear()
  const mes = dia.getMonth() + 1
  const d = dia.getDate()

  if (FERIADOS_FIXOS.some(([m, dd]) => m === mes && dd === d)) return true
  // Consciência Negra: nacional desde 2024 (Lei 14.759/2023).
  if (ano >= 2024 && mes === 11 && d === 20) return true
  // Sexta-feira da Paixão.
  const paixao = pascoa(ano)
  paixao.setDate(paixao.getDate() - 2)

  return chave(paixao) === chave(dia)
}

/** Recesso forense: de 20 de dezembro a 20 de janeiro, inclusive. O prazo não corre. */
export function isRecesso(dia: Date): boolean {
  const mes = dia.getMonth() + 1
  const d = dia.getDate()

  return (mes === 12 && d >= 20) || (mes === 1 && d <= 20)
}

export function isDiaUtil(dia: Date): boolean {
  const semana = dia.getDay()

  return semana !== 0 && semana !== 6 && !isFeriadoNacional(dia)
}

export function addBusinessDays(inicio: Date, dias: number): Date {
  const data = new Date(inicio.getFullYear(), inicio.getMonth(), inicio.getDate())
  let restantes = Math.max(0, Math.trunc(dias))

  while (restantes > 0) {
    data.setDate(data.getDate() + 1)
    if (isDiaUtil(data) && !isRecesso(data)) restantes -= 1
  }

  return data
}
