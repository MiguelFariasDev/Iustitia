import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import type { ProcessoFormValues } from './schemas'
import type { AreaDireito } from './types'

/** Como o backend devolve (Legal.Api). Traduzido logo abaixo para o modelo da tela. */
interface ConsultaTribunalDto {
  found: boolean
  tribunal: string | null
  processo: {
    numeroProcesso: string
    tribunal: string
    grau: string | null
    dataAjuizamento: string | null
    emSegredoDeJustica: boolean
    classe: string | null
    sistema: string | null
    orgaoJulgador: string | null
    assuntos: string[]
    movimentos: { nome: string; dataHora: string | null }[]
    codigoMunicipioIbge: number | null
  } | null
}

export interface MovimentoTribunal {
  nome: string
  dataHora: string | null
}

export interface ProcessoTribunal {
  numeroProcesso: string
  tribunal: string
  grau: string | null
  dataAjuizamento: string | null
  emSegredoDeJustica: boolean
  classe: string | null
  sistema: string | null
  orgaoJulgador: string | null
  assuntos: string[]
  movimentos: MovimentoTribunal[]
  codigoMunicipioIbge: number | null
}

export interface ConsultaTribunalResult {
  found: boolean
  tribunal: string | null
  processo: ProcessoTribunal | null
}

/**
 * Consulta os metadados do processo no tribunal (DataJud, via nosso backend).
 *
 * `found: false` NÃO é erro: o tribunal respondeu e não conhece o processo. Só o que
 * impede o cadastro — número inválido, tribunal fora do DataJud, serviço fora do ar —
 * chega como ApiError, e aí o formulário segue no preenchimento manual.
 */
export async function consultarProcessoTribunal(
  cnjNumber: string
): Promise<ConsultaTribunalResult> {
  const { data } = await apiClient.post<ConsultaTribunalDto>(
    endpoints.processos.consultarTribunal,
    {
      cnjNumber,
    }
  )

  return {
    found: data.found,
    tribunal: data.tribunal,
    processo: data.processo
      ? {
          ...data.processo,
          assuntos: data.processo.assuntos ?? [],
          movimentos: data.processo.movimentos ?? [],
          codigoMunicipioIbge: data.processo.codigoMunicipioIbge ?? null,
        }
      : null,
  }
}

/**
 * Área do direito inferida do segmento da Justiça (dígito J do número CNJ). Só os segmentos
 * que determinam a área sem ambiguidade: a Justiça do Trabalho é sempre trabalhista, e a
 * Eleitoral, administrativa/eleitoral. Estadual e Federal misturam áreas — ficam de fora
 * para a pessoa escolher, em vez de o sistema chutar.
 */
export function areaFromTribunal(tribunal: string | null): AreaDireito | null {
  if (!tribunal) return null
  if (tribunal.startsWith('TRT') || tribunal === 'TST') return 'Trabalhista'
  if (tribunal.startsWith('TRE-') || tribunal === 'TSE') return 'Administrativo'
  return null
}

/** Grau de jurisdição com nome legível; códigos desconhecidos passam como vieram. */
const graus: Record<string, string> = {
  G1: '1º grau',
  G2: '2º grau',
  JE: 'Juizado Especial',
  TR: 'Turma Recursal',
  SUP: 'Tribunal superior',
}

/**
 * Campos do formulário que a consulta consegue preencher.
 *
 * Só devolve o que o tribunal informou: campo vazio na resposta NÃO sobrescreve o que a
 * pessoa já digitou. Cliente e responsável nunca vêm daqui — são decisão do escritório, não
 * do tribunal. Valor da causa e parte contrária também não: a API pública não os expõe.
 *
 * Mapeamento DataJud -> Iustitia:
 *   tribunal            -> tribunal
 *   orgaoJulgador.nome  -> vara
 *   dataAjuizamento     -> dataDistribuicao
 *   classe + assunto[0] -> assunto
 *   segmento da Justiça -> área do direito (só Trabalho e Eleitoral)
 *   grau/sistema/formato/sigilo + movimento mais recente -> observações
 */
export function toFormPatch(processo: ProcessoTribunal): Partial<ProcessoFormValues> {
  const patch: Partial<ProcessoFormValues> = {}

  if (processo.tribunal) patch.tribunal = processo.tribunal
  if (processo.orgaoJulgador) patch.vara = processo.orgaoJulgador
  if (processo.dataAjuizamento) patch.dataDistribuicao = processo.dataAjuizamento

  // Assunto da tela é texto livre; o tribunal manda classe e assuntos separados. A classe
  // processual é o que identifica o caso ("Procedimento Comum"), então ela vem primeiro.
  const assunto = [processo.classe, processo.assuntos[0]].filter(Boolean).join(' — ')
  if (assunto) patch.assunto = assunto

  const area = areaFromTribunal(processo.tribunal)
  if (area) patch.area = area

  const observacoes = montarObservacoes(processo)
  if (observacoes) patch.observacoes = observacoes

  return patch
}

/** Dados que o sistema ainda não tem campo próprio para guardar, registrados como nota. */
function montarObservacoes(processo: ProcessoTribunal): string {
  const linhas: string[] = []

  const grau = processo.grau ? (graus[processo.grau] ?? processo.grau) : null
  const origem = [grau, processo.sistema].filter(Boolean).join(' · ')
  if (origem) linhas.push(`Consulta DataJud — ${origem}`)

  if (processo.assuntos.length > 1) linhas.push(`Assuntos: ${processo.assuntos.join('; ')}`)
  if (processo.emSegredoDeJustica) linhas.push('Processo em segredo de justiça.')

  const ultimo = processo.movimentos[0]
  if (ultimo) {
    const data = ultimo.dataHora
      ? ` (${ultimo.dataHora.slice(0, 10).split('-').reverse().join('/')})`
      : ''
    linhas.push(`Último movimento: ${ultimo.nome}${data}`)
  }

  return linhas.join('\n')
}
