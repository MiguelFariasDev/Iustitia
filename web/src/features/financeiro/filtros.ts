import type { Lancamento, PeriodoFinanceiro, StatusLancamento } from './types'

/** Filtros da tela, aplicados no cliente sobre a lista já carregada. */
export function filtrarLancamentos(
  lancamentos: Lancamento[],
  filtros: { status: StatusLancamento | null; periodo: PeriodoFinanceiro },
  agora: Date = new Date()
): Lancamento[] {
  const fimDoDia = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59)
  const limite30 = fimDoDia(new Date(agora.getTime() + 30 * 86_400_000))

  return lancamentos.filter((l) => {
    if (filtros.status && l.status !== filtros.status) return false

    const venc = new Date(l.vencimento)
    if (filtros.periodo === 'mes') {
      return venc.getFullYear() === agora.getFullYear() && venc.getMonth() === agora.getMonth()
    }
    if (filtros.periodo === 'proximos30') {
      return (
        venc >= new Date(agora.getFullYear(), agora.getMonth(), agora.getDate()) && venc <= limite30
      )
    }

    return true
  })
}
