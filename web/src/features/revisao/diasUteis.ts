/**
 * Soma `dias` dias ÚTEIS a `inicio`, pulando sábados e domingos.
 *
 * ATENÇÃO: NÃO conhece feriados, recesso forense nem suspensão de prazos. É uma estimativa para
 * o advogado conferir — nunca a fonte de um prazo processual. A tela diz isso ao lado da data.
 *
 * Segue a regra do CPC art. 224: exclui o dia do começo (a contagem começa no primeiro dia útil
 * seguinte) e, se o último dia cair em dia sem expediente, prorroga — aqui, por só considerar
 * dias úteis, o resultado já cai em dia útil.
 */
export function addBusinessDays(inicio: Date, dias: number): Date {
  const data = new Date(inicio.getFullYear(), inicio.getMonth(), inicio.getDate())
  let restantes = Math.max(0, Math.trunc(dias))

  while (restantes > 0) {
    data.setDate(data.getDate() + 1)
    const diaSemana = data.getDay()
    if (diaSemana !== 0 && diaSemana !== 6) restantes -= 1
  }

  return data
}
