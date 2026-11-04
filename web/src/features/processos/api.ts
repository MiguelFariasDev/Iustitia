import type { AxiosInstance } from 'axios'
import { apiClient } from '@/lib/api/client'
import { endpoints } from '@/lib/api/endpoints'
import { onlyDigits } from '@/lib/validation/documento'
import { toProcesso, toProcessoListItem, type ListProcessosDto, type ProcessoDto } from './contract'
import { parseValor, type ProcessoFormValues } from './schemas'
import type { Processo, ProcessosFilters, ProcessosPage, StatusProcesso } from './types'

export async function listProcessos(
  filters: ProcessosFilters = {},
  client: AxiosInstance = apiClient
): Promise<ProcessosPage> {
  const { data } = await client.get<ListProcessosDto>(endpoints.processos.list, {
    params: {
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? 20,
      clienteId: filters.clienteId ?? undefined,
      area: filters.area ?? undefined,
      status: filters.status ?? undefined,
      busca: filters.busca || undefined,
      responsavelUserId: filters.responsavelUserId ?? undefined,
      tribunal: filters.tribunal || undefined,
      distribuicaoDe: filters.distribuicaoDe || undefined,
      distribuicaoAte: filters.distribuicaoAte || undefined,
    },
  })

  return { ...data, items: data.items.map(toProcessoListItem) }
}

export async function getProcessoById(
  id: string,
  client: AxiosInstance = apiClient
): Promise<Processo> {
  const { data } = await client.get<ProcessoDto>(endpoints.processos.byId(id))

  return toProcesso(data)
}

export async function createProcesso(values: ProcessoFormValues): Promise<Processo> {
  const { data } = await apiClient.post<ProcessoDto>(endpoints.processos.list, {
    ...toCommonPayload(values),
    // O backend espera o número sem máscara.
    cnjNumber: onlyDigits(values.cnjNumber),
  })

  return toProcesso(data)
}

export async function updateProcesso(
  id: string,
  values: ProcessoFormValues,
  responsavelUserId: string
): Promise<void> {
  // O CNJ não é enviado na edição: é a identidade do processo e a chave pela qual as
  // publicações já capturadas foram vinculadas (ver Processo.Update).
  await apiClient.put(endpoints.processos.byId(id), {
    ...toCommonPayload(values),
    responsavelUserId,
  })
}

export async function changeProcessoStatus(id: string, status: StatusProcesso): Promise<void> {
  await apiClient.patch(endpoints.processos.status(id), { status })
}

export async function archiveProcesso(id: string): Promise<void> {
  await apiClient.post(endpoints.processos.archive(id))
}

/**
 * Campos comuns a criação e edição. O número CNJ fica de fora de propósito: só a criação o
 * envia (ver updateProcesso). Separar assim obriga a decidir conscientemente onde um campo
 * novo entra, em vez de ele vazar para o update.
 */
function toCommonPayload(values: ProcessoFormValues) {
  return {
    clienteId: values.clienteId,
    area: values.area,
    assunto: nullIfBlank(values.assunto),
    parteContraria: nullIfBlank(values.parteContraria),
    valorCausa: values.valorCausa ? parseValor(values.valorCausa) : null,
    dataDistribuicao: nullIfBlank(values.dataDistribuicao),
    observacoes: nullIfBlank(values.observacoes),
    foro: {
      tribunal: nullIfBlank(values.tribunal),
      comarca: nullIfBlank(values.comarca),
      vara: nullIfBlank(values.vara),
    },
  }
}

function nullIfBlank(value: string | undefined) {
  return value && value.trim() ? value.trim() : null
}
